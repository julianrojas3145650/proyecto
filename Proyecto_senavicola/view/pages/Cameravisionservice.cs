using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;
using Tesseract;
using OCV = OpenCvSharp;

namespace Proyecto_senavicola.view.pages
{
    // ================================================================
    //  RESULTADO DEL ANÁLISIS DE VISIÓN
    // ================================================================

    public class ResultadoAnalisis
    {
        public double DiametroCm { get; set; }
        public double LargoCm { get; set; }
        public double VolumenCm3 { get; set; }   // mediana estabilizada
        public double VolumenBruto { get; set; }   // valor crudo del frame
        public double PxPorCm { get; set; }
        public int CuadrosDetect { get; set; }
        public string TextoOCR { get; set; }
        public bool HuevoDetectado { get; set; }   // contorno encontrado
        public bool MedicionValida { get; set; }   // escala + rango físico ok
        public string ModoEscala { get; set; }
        public string DiagEscala { get; set; }   // info de depuración de escala
        public double PesoEstable { get; set; }
        public bool PesoConfirmado { get; set; }
    }

    // ================================================================
    //  SERVICIO DE CÁMARA Y VISIÓN
    // ================================================================

    public class CameraVisionService : IDisposable
    {
        private readonly System.Windows.Threading.Dispatcher _dispatcher;
        private readonly Border _borderCamera;
        private readonly System.Windows.Shapes.Ellipse _statusCamera;
        private readonly Button _btnActivarCamara;

        private FilterInfoCollection _videoDevices;
        private VideoCaptureDevice _videoSource;
        private System.Windows.Controls.Image _imagenCamara;
        private EggVisionProcessor _eggProcessor;
        private bool _camaraActiva = false;
        private bool _isDisposing = false;
        private readonly object _cameraLock = new object();

        public event Action<BitmapImage, ResultadoAnalisis> NuevoFrame;
        public bool CamaraActiva => _camaraActiva;
        public ResultadoAnalisis UltimoResultado => _eggProcessor?.UltimoResultado;

        public CameraVisionService(
            System.Windows.Threading.Dispatcher dispatcher,
            Border borderCamera,
            System.Windows.Shapes.Ellipse statusCamera,
            Button btnActivarCamara)
        {
            _dispatcher = dispatcher;
            _borderCamera = borderCamera;
            _statusCamera = statusCamera;
            _btnActivarCamara = btnActivarCamara;
            _eggProcessor = new EggVisionProcessor();
        }

        public void InicializarCamara()
        {
            try { _videoDevices = null; }
            catch { }
        }

        public void IniciarCamara(Window ownerWindow)
        {
            if (_camaraActiva) return;
            try
            {
                _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (_videoDevices.Count == 0)
                {
                    MessageBox.Show("No se detectaron cámaras.", "Sin Cámara",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dlg = new view.dialogs.SeleccionCamaraDialog(_videoDevices);
                dlg.Owner = ownerWindow;
                if (dlg.ShowDialog() == true && dlg.CamaraSeleccionada != null)
                {
                    _videoSource = new VideoCaptureDevice(dlg.CamaraSeleccionada.MonikerString);

                    if (_videoSource.VideoCapabilities != null &&
                        _videoSource.VideoCapabilities.Length > 0)
                    {
                        foreach (var cap in _videoSource.VideoCapabilities)
                            System.Diagnostics.Debug.WriteLine(
                                $"[Cámara] {cap.FrameSize.Width}×{cap.FrameSize.Height} " +
                                $"@ {cap.AverageFrameRate} fps");

                        var mejorRes = _videoSource.VideoCapabilities
                            .Where(c => c.FrameSize.Width <= 1280)
                            .OrderByDescending(c => c.AverageFrameRate)
                            .First();

                        _videoSource.VideoResolution = mejorRes;
                        System.Diagnostics.Debug.WriteLine(
                            $"[Cámara] Resolución elegida: " +
                            $"{mejorRes.FrameSize.Width}×{mejorRes.FrameSize.Height} " +
                            $"@ {mejorRes.AverageFrameRate} fps");
                    }

                    _videoSource.NewFrame += VideoSource_NewFrame;
                    _imagenCamara = new System.Windows.Controls.Image
                    {
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                        RenderTransform = new ScaleTransform(1, 1)
                    };
                    _borderCamera.Child = _imagenCamara;
                    _videoSource.Start();
                    _camaraActiva = true;

                    ActualizarIndicadorUI(activa: true);
                    if (_btnActivarCamara != null)
                        _btnActivarCamara.Content = "📷 Desactivar Cámara";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al activar la cámara:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                DetenerCamara();
            }
        }

        public void DetenerCamara()
        {
            lock (_cameraLock)
            {
                try
                {
                    if (_videoSource != null && _videoSource.IsRunning)
                    {
                        _videoSource.NewFrame -= VideoSource_NewFrame;
                        _videoSource.SignalToStop();
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            try { _videoSource?.WaitForStop(); } catch { }
                        }).Wait(2000);
                    }
                    if (_videoSource != null)
                    {
                        try { _videoSource.NewFrame -= VideoSource_NewFrame; } catch { }
                        _videoSource = null;
                    }
                    if (!_isDisposing)
                        _dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { if (_imagenCamara != null) _imagenCamara.Source = null; }
                            catch { }
                        }));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al detener cámara: {ex.Message}");
                }
            }

            _camaraActiva = false;
            _dispatcher.BeginInvoke(new Action(() =>
            {
                ActualizarIndicadorUI(activa: false);
                if (_btnActivarCamara != null)
                    _btnActivarCamara.Content = "📷 Activar Cámara";
                MostrarPantallaDesactivada();
            }));
        }

        public void MarcarDisposing()
        {
            _isDisposing = true;
            DetenerCamara();
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (_isDisposing || !_camaraActiva) return;
            try
            {
                using (var bitmapOriginal = (System.Drawing.Bitmap)eventArgs.Frame.Clone())
                {
                    using var bitmapFinal = _eggProcessor != null
                        ? _eggProcessor.ProcesarFrame(bitmapOriginal)
                        : (System.Drawing.Bitmap)bitmapOriginal.Clone();

                    var bi = ConvertirBitmapABitmapImage(bitmapFinal);

                    _dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_camaraActiva || _isDisposing) return;
                        try
                        {
                            if (_imagenCamara?.IsLoaded == true)
                            {
                                _imagenCamara.Source = bi;
                                NuevoFrame?.Invoke(bi, _eggProcessor?.UltimoResultado);
                            }
                        }
                        catch { }
                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
            }
            catch { }
        }

        private BitmapImage ConvertirBitmapABitmapImage(System.Drawing.Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
        }

        private void ActualizarIndicadorUI(bool activa)
        {
            try
            {
                if (_statusCamera != null)
                    _statusCamera.Fill = new SolidColorBrush(
                        activa ? Color.FromRgb(76, 175, 80)
                               : Color.FromRgb(220, 53, 69));
            }
            catch { }
        }

        private void MostrarPantallaDesactivada()
        {
            try
            {
                var sp = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                sp.Children.Add(new TextBlock
                {
                    Text = "📷",
                    FontSize = 80,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 20)
                });
                sp.Children.Add(new TextBlock
                {
                    Text = "Cámara Desactivada",
                    FontSize = 18,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                _borderCamera.Child = sp;
            }
            catch { }
        }

        public void Dispose()
        {
            _isDisposing = true;
            DetenerCamara();
            _eggProcessor?.Dispose();
            _eggProcessor = null;
        }
    }

    // ================================================================
    //  PROCESADOR DE VISIÓN POR COMPUTADORA
    // ================================================================

    public class EggVisionProcessor : IDisposable
    {
        private const double DIAMETRO_HOJA_CM = 20.0;
        private const double MM_POR_CM = 10.0;

        private const double ROI_X1 = 0.15;
        private const double ROI_Y1 = 0.65;
        private const double ROI_X2 = 0.45;
        private const double ROI_Y2 = 0.80;

        private const double VISION_Y_MAX = 0.62;

        private const double EGG_LARGO_MIN = 4.0;
        private const double EGG_LARGO_MAX = 7.2;
        private const double EGG_DIAM_MIN = 3.0;
        private const double EGG_DIAM_MAX = 5.8;
        private const double EGG_VOL_MIN = 25.0;
        private const double EGG_VOL_MAX = 110.0;
        private const double EGG_LARGO_PROM = 5.8;

        private const double AR_MIN = 0.52;
        private const double AR_MAX = 0.93;
        private const double CIRC_MIN = 0.45;
        private const double AREA_MIN = 0.006;
        private const double AREA_MAX = 0.50;

        private const int BUF_VOL = 10;
        private const int BUF_PESO = 7;
        private const int REP_MIN = 4;
        private const double TOL_PESO = 1.5;
        private const int OCR_MS = 800;

        private TesseractEngine _tesseract;
        private readonly object _tLock = new object();
        private DateTime _lastOcr = DateTime.MinValue;
        private string _ocrCache = "";
        private bool _disposed;
        private readonly Queue<double> _qVol = new Queue<double>();
        private readonly Queue<double> _qPeso = new Queue<double>();
        private double _pesoEstable;
        private bool _pesoOk;

        public ResultadoAnalisis UltimoResultado { get; private set; } = new ResultadoAnalisis();

        public EggVisionProcessor() => InicializarTesseract();

        // ════════════════════════════════════════════════════════════
        //  FRAME PRINCIPAL  ← CORREGIDO: try/finally en lugar de using
        // ════════════════════════════════════════════════════════════

        public System.Drawing.Bitmap ProcesarFrame(System.Drawing.Bitmap src)
        {
            var frame = (System.Drawing.Bitmap)src.Clone();

            OCV.Mat mat = null;
            OCV.Mat matTop = null;
            OCV.Mat matOCR = null;

            try
            {
                mat = BitmapToMat(frame);
                matTop = CropTop(mat);

                var (contorno, elipse, rect, huevoOk) = DetectarHuevo(matTop);
                var (pxPorCm, modoEscala, diagEscala) = DetectarEscalaConValidacion(matTop, rect, huevoOk);

                double dCm = 0, lCm = 0, volBruto = 0, volEstable = 0;
                int mm = 0;
                bool medOk = false;

                if (huevoOk && pxPorCm > 0)
                {
                    if (elipse.HasValue)
                    {
                        double e1 = elipse.Value.Size.Width / 2.0;
                        double e2 = elipse.Value.Size.Height / 2.0;
                        double aCm = Math.Max(e1, e2) / pxPorCm;
                        double bCm = Math.Min(e1, e2) / pxPorCm;
                        lCm = aCm * 2;
                        dCm = bCm * 2;
                        volBruto = (4.0 / 3.0) * Math.PI * aCm * bCm * bCm;
                    }
                    else
                    {
                        dCm = rect.Width / pxPorCm;
                        lCm = rect.Height / pxPorCm;
                        double a = lCm / 2.0, b = dCm / 2.0;
                        volBruto = (4.0 / 3.0) * Math.PI * a * b * b;
                    }

                    medOk = lCm >= EGG_LARGO_MIN && lCm <= EGG_LARGO_MAX &&
                            dCm >= EGG_DIAM_MIN && dCm <= EGG_DIAM_MAX;

                    if (medOk)
                    {
                        mm = (int)Math.Round(lCm * MM_POR_CM);
                        volEstable = PushVol(volBruto);
                    }
                    else
                    {
                        volEstable = _qVol.Count > 0 ? Mediana(_qVol.ToList()) : 0;
                        System.Diagnostics.Debug.WriteLine(
                            $"[v4] Medición descartada: largo={lCm:F2}cm diam={dCm:F2}cm px/cm={pxPorCm:F1}");
                    }
                }
                else
                {
                    _qVol.Clear();
                }

                // ── OCR: clonar mat ANTES de que se libere ────────────
                if ((DateTime.Now - _lastOcr).TotalMilliseconds > OCR_MS)
                {
                    var clone = mat.Clone();

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        string t = EjecutarOCR(clone);
                        PushPeso(t);
                        _ocrCache = t;
                        clone.Dispose();
                    });

                    _lastOcr = DateTime.Now;
                }

                DibujarFrame(frame, contorno, elipse, rect, huevoOk, medOk,
                             pxPorCm, dCm, lCm, volEstable, volBruto, mm,
                             _ocrCache, modoEscala, diagEscala);

                UltimoResultado = new ResultadoAnalisis
                {
                    DiametroCm = Math.Round(dCm, 2),
                    LargoCm = Math.Round(lCm, 2),
                    VolumenCm3 = Math.Round(volEstable, 3),
                    VolumenBruto = Math.Round(volBruto, 3),
                    PxPorCm = Math.Round(pxPorCm, 2),
                    CuadrosDetect = mm,
                    TextoOCR = _ocrCache,
                    HuevoDetectado = huevoOk,
                    MedicionValida = huevoOk && medOk,
                    ModoEscala = modoEscala,
                    DiagEscala = diagEscala,
                    PesoEstable = _pesoEstable,
                    PesoConfirmado = _pesoOk
                };
            }
            finally
            {
                // Liberar en orden inverso, de más derivado a más base
                matOCR?.Dispose();
                matTop?.Dispose();
                mat?.Dispose();
            }

            return frame;
        }

        // ════════════════════════════════════════════════════════════
        //  DETECCIÓN DE ESCALA
        // ════════════════════════════════════════════════════════════

        private (double px, string modo, string diag) DetectarEscalaConValidacion(
            OCV.Mat mat, OCV.Rect rectHuevo, bool huevoOk)
        {
            var candidatos = new List<(double px, string modo)>();

            double px1mm = MedirEscalaCuadricula(mat, 1.0);
            double px5mm = MedirEscalaCuadricula(mat, 5.0);

            if (px1mm > 5) candidatos.Add((px1mm, "Cuadrícula 1mm"));
            if (px5mm > 5) candidatos.Add((px5mm, "Cuadrícula 5mm"));

            var ampliados = new List<(double px, string modo)>();
            foreach (var (px, modo) in candidatos)
            {
                foreach (int f in new[] { 1, 2, 3, 4, 5 })
                {
                    double pxF = px * f;
                    if (pxF < 15 || pxF > 350) continue;
                    ampliados.Add((pxF, f == 1 ? modo : $"{modo} ×{f}"));
                }
                foreach (int f in new[] { 2, 3, 4, 5 })
                {
                    double pxF = px / f;
                    if (pxF < 15 || pxF > 350) continue;
                    ampliados.Add((pxF, $"{modo} ÷{f}"));
                }
            }

            if (huevoOk && rectHuevo.Width > 0 && rectHuevo.Height > 0)
            {
                double ejeMayorPx = Math.Max(rectHuevo.Width, rectHuevo.Height);

                foreach (var (pxC, modoC) in ampliados.OrderBy(x => x.px))
                {
                    double largo = ejeMayorPx / pxC;
                    if (largo >= EGG_LARGO_MIN && largo <= EGG_LARGO_MAX)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[v4 Escala] Candidato aceptado: {pxC:F1} px/cm ({modoC}) → largo={largo:F2}cm");
                        return (pxC, modoC, $"OK ejeMayor={ejeMayorPx:F0}px");
                    }
                }

                double pxInferido = ejeMayorPx / EGG_LARGO_PROM;
                if (pxInferido > 15 && pxInferido < 350)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[v4 Escala] Inferida desde huevo: {pxInferido:F1} px/cm");
                    return (pxInferido, "Inferida (huevo)",
                            $"ejeMayor={ejeMayorPx:F0}px÷{EGG_LARGO_PROM}cm");
                }
            }

            double pxHoja = MedirEscalaHoja(mat, DIAMETRO_HOJA_CM);
            if (pxHoja > 5)
                return (pxHoja, "Hoja circular", "OK");

            string diagFallback = $"px1mm={px1mm:F1} px5mm={px5mm:F1} candidatos={ampliados.Count}";
            System.Diagnostics.Debug.WriteLine($"[v4 Escala] Fallback 60px/cm — {diagFallback}");
            return (60.0, "Default (60px/cm)", diagFallback);
        }

        private double MedirEscalaCuadricula(OCV.Mat mat, double mmPorLinea)
        {
            try
            {
                using var gray = new OCV.Mat();
                using var blur = new OCV.Mat();
                using var edges = new OCV.Mat();

                OCV.Cv2.CvtColor(mat, gray, OCV.ColorConversionCodes.BGR2GRAY);
                OCV.Cv2.GaussianBlur(gray, blur, new OCV.Size(3, 3), 0);
                OCV.Cv2.Canny(blur, edges, 15, 50);

                int thr = mmPorLinea < 2 ? 20 : 40;
                double minLen = mmPorLinea < 2 ? 12 : 25;
                double maxGap = mmPorLinea < 2 ? 5 : 12;

                var ls = OCV.Cv2.HoughLinesP(edges, 1, Math.PI / 180, thr, minLen, maxGap);
                if (ls == null || ls.Length < 6) return 0;

                var posH = new List<double>();
                var posV = new List<double>();

                foreach (var l in ls)
                {
                    double ang = Math.Abs(
                        Math.Atan2(l.P2.Y - l.P1.Y, l.P2.X - l.P1.X) * 180 / Math.PI);
                    if (ang < 10 || ang > 170) posH.Add((l.P1.Y + l.P2.Y) / 2.0);
                    else if (ang > 80 && ang < 100) posV.Add((l.P1.X + l.P2.X) / 2.0);
                }

                double espH = EspacioMediano(posH);
                double espV = EspacioMediano(posV);

                double esp = 0;
                if (espH > 2 && espV > 2) esp = (espH + espV) / 2.0;
                else if (espH > 2) esp = espH;
                else if (espV > 2) esp = espV;

                if (esp < 2) return 0;

                double pxPorCm = esp / (mmPorLinea / MM_POR_CM);
                return pxPorCm < 12 || pxPorCm > 280 ? 0 : pxPorCm;
            }
            catch { return 0; }
        }

        private double MedirEscalaHoja(OCV.Mat mat, double diametroCm)
        {
            try
            {
                using var gray = new OCV.Mat();
                using var blur = new OCV.Mat();
                OCV.Cv2.CvtColor(mat, gray, OCV.ColorConversionCodes.BGR2GRAY);
                OCV.Cv2.GaussianBlur(gray, blur, new OCV.Size(9, 9), 2);
                var cs = OCV.Cv2.HoughCircles(blur, OCV.HoughModes.Gradient,
                    1.2, mat.Width * 0.3, 80, 40, mat.Width / 6, mat.Width / 2);
                if (cs == null || cs.Length == 0) return 0;
                return (cs.Max(c => c.Radius) * 2) / diametroCm;
            }
            catch { return 0; }
        }

        private double EspacioMediano(List<double> pos)
        {
            if (pos.Count < 4) return 0;
            pos.Sort();

            var u = new List<double> { pos[0] };
            foreach (var p in pos)
                if (p - u[u.Count - 1] > 2) u.Add(p);
            if (u.Count < 4) return 0;

            var difs = new List<double>();
            for (int i = 1; i < u.Count; i++)
            {
                double d = u[i] - u[i - 1];
                if (d > 2 && d < 300) difs.Add(d);
            }
            if (difs.Count < 3) return 0;

            difs.Sort();
            double med = difs[difs.Count / 2];

            int ok = difs.Count(d => Math.Abs(d - med) / med < 0.35);
            return (double)ok / difs.Count < 0.55 ? 0 : med;
        }

        // ════════════════════════════════════════════════════════════
        //  DETECCIÓN DEL HUEVO
        // ════════════════════════════════════════════════════════════

        private (OCV.Point[] c, OCV.RotatedRect? el, OCV.Rect r, bool ok)
            DetectarHuevo(OCV.Mat mat)
        {
            try
            {
                using var gray = new OCV.Mat();
                using var blur = new OCV.Mat();
                using var th = new OCV.Mat();
                using var mor = new OCV.Mat();

                OCV.Cv2.CvtColor(mat, gray, OCV.ColorConversionCodes.BGR2GRAY);
                OCV.Cv2.GaussianBlur(gray, blur, new OCV.Size(9, 9), 2);
                OCV.Cv2.AdaptiveThreshold(blur, th, 255,
                    OCV.AdaptiveThresholdTypes.GaussianC,
                    OCV.ThresholdTypes.BinaryInv, 21, 4);

                var k = OCV.Cv2.GetStructuringElement(
                    OCV.MorphShapes.Ellipse, new OCV.Size(7, 7));
                OCV.Cv2.MorphologyEx(th, mor, OCV.MorphTypes.Close, k, iterations: 3);
                OCV.Cv2.MorphologyEx(mor, mor, OCV.MorphTypes.Open, k, iterations: 2);
                k.Dispose();

                OCV.Cv2.FindContours(mor, out var cs, out _,
                    OCV.RetrievalModes.External,
                    OCV.ContourApproximationModes.ApproxSimple);

                return MejorContorno(cs, mat.Rows * mat.Cols);
            }
            catch { return (null, null, default, false); }
        }

        private (OCV.Point[] c, OCV.RotatedRect? el, OCV.Rect r, bool ok)
            MejorContorno(OCV.Point[][] cs, int imgArea)
        {
            if (cs == null || cs.Length == 0) return (null, null, default, false);

            OCV.Point[] best = null;
            OCV.RotatedRect? elB = null;
            double score = 0;
            OCV.Rect rect = default;

            foreach (var c in cs)
            {
                double a = OCV.Cv2.ContourArea(c);
                if (a < imgArea * AREA_MIN || a > imgArea * AREA_MAX) continue;

                var br = OCV.Cv2.BoundingRect(c);
                double ar = (double)Math.Min(br.Width, br.Height)
                           / Math.Max(br.Width, br.Height);
                if (ar < AR_MIN || ar > AR_MAX) continue;

                double per = OCV.Cv2.ArcLength(c, true);
                if (per < 1) continue;
                double cir = 4 * Math.PI * a / (per * per);
                if (cir < CIRC_MIN) continue;

                double fill = a / ((double)br.Width * br.Height + 1);
                if (fill < 0.35) continue;

                double sc = a * cir;
                if (sc <= score) continue;

                OCV.RotatedRect? elOpt = null;
                if (c.Length >= 5)
                {
                    try
                    {
                        var el = OCV.Cv2.FitEllipse(c);
                        if (el.Size.Width > 5 && el.Size.Height > 5)
                        {
                            double elAr = Math.Min(el.Size.Width, el.Size.Height)
                                        / Math.Max(el.Size.Width, el.Size.Height);
                            if (elAr >= AR_MIN && elAr <= AR_MAX) elOpt = el;
                        }
                    }
                    catch { }
                }

                score = sc; best = c; elB = elOpt; rect = br;
            }

            if (best == null) return (null, null, default, false);
            return (best, elB, rect, true);
        }

        // ════════════════════════════════════════════════════════════
        //  OCR — DISPLAY SF-400  ← CORREGIDO: try/finally en todo
        // ════════════════════════════════════════════════════════════

        private string EjecutarOCR(OCV.Mat mat)
        {
            // NOTA: mat es un clone propiedad del llamador; no lo liberamos aquí.
            try
            {
                int rx1 = Clamp((int)(mat.Width * ROI_X1), 0, mat.Width - 10);
                int ry1 = Clamp((int)(mat.Height * ROI_Y1), 0, mat.Height - 10);
                int rx2 = Clamp((int)(mat.Width * ROI_X2), rx1 + 10, mat.Width);
                int ry2 = Clamp((int)(mat.Height * ROI_Y2), ry1 + 10, mat.Height);

                var roiRect = new OCV.Rect(rx1, ry1, rx2 - rx1, ry2 - ry1);

                // Todos los Mat intermedios se declaran fuera para poder liberarlos
                // en el bloque finally aunque ocurra una excepción en mitad del método.
                OCV.Mat roi = null;
                OCV.Mat roiX4 = null;
                OCV.Mat gray = null;

                var mascaras = new List<(OCV.Mat m, string nombre)>();

                try
                {
                    roi = new OCV.Mat(mat, roiRect).Clone();
                    roiX4 = new OCV.Mat();
                    gray = new OCV.Mat();

                    OCV.Cv2.Resize(roi, roiX4,
                        new OCV.Size(roi.Width * 4, roi.Height * 4),
                        interpolation: OCV.InterpolationFlags.Lanczos4);

                    OCV.Cv2.CvtColor(roiX4, gray, OCV.ColorConversionCodes.BGR2GRAY);

                    // ── Método A: diferencia G–R ──────────────────────
                    {
                        OCV.Mat[] ch = null;
                        OCV.Mat diff = null;
                        try
                        {
                            ch = OCV.Cv2.Split(roiX4);
                            diff = new OCV.Mat();
                            OCV.Cv2.Subtract(ch[1], ch[2], diff);   // G - R

                            foreach (int t in new[] { 3, 6, 10, 15 })
                            {
                                var mA = new OCV.Mat();
                                OCV.Cv2.Threshold(diff, mA, t, 255, OCV.ThresholdTypes.Binary);
                                mascaras.Add((mA, $"G-R_thr{t}"));
                            }
                        }
                        finally
                        {
                            diff?.Dispose();
                            if (ch != null) foreach (var c in ch) c?.Dispose();
                        }
                    }

                    // ── Método B: CLAHE + Otsu ────────────────────────
                    {
                        OCV.CLAHE clahe = null;
                        OCV.Mat enh = null;
                        try
                        {
                            clahe = OCV.Cv2.CreateCLAHE(8.0, new OCV.Size(4, 4));
                            enh = new OCV.Mat();
                            clahe.Apply(gray, enh);

                            var mB = new OCV.Mat();
                            OCV.Cv2.Threshold(enh, mB, 0, 255,
                                OCV.ThresholdTypes.Binary | OCV.ThresholdTypes.Otsu);
                            mascaras.Add((mB, "CLAHE+Otsu"));

                            var mBi = new OCV.Mat();
                            OCV.Cv2.Threshold(enh, mBi, 0, 255,
                                OCV.ThresholdTypes.BinaryInv | OCV.ThresholdTypes.Otsu);
                            mascaras.Add((mBi, "CLAHE+OtsuInv"));
                        }
                        finally
                        {
                            enh?.Dispose();
                            clahe?.Dispose();
                        }
                    }

                    // ── Método C: threshold fijo ──────────────────────
                    foreach (int thr in new[] { 20, 35, 50, 70, 90, 110, 130 })
                    {
                        var mC = new OCV.Mat();
                        OCV.Cv2.Threshold(gray, mC, thr, 255, OCV.ThresholdTypes.Binary);
                        mascaras.Add((mC, $"Fixed{thr}"));

                        var mCi = new OCV.Mat();
                        OCV.Cv2.Threshold(gray, mCi, thr, 255, OCV.ThresholdTypes.BinaryInv);
                        mascaras.Add((mCi, $"FixedInv{thr}"));
                    }

                    // ── Método D: threshold adaptativo ────────────────
                    {
                        OCV.Mat blurAdapt = null;
                        try
                        {
                            blurAdapt = new OCV.Mat();
                            OCV.Cv2.GaussianBlur(gray, blurAdapt, new OCV.Size(3, 3), 0);

                            var mD = new OCV.Mat();
                            OCV.Cv2.AdaptiveThreshold(blurAdapt, mD, 255,
                                OCV.AdaptiveThresholdTypes.GaussianC,
                                OCV.ThresholdTypes.Binary, 15, -2);
                            mascaras.Add((mD, "Adaptive"));
                        }
                        finally { blurAdapt?.Dispose(); }
                    }

                    // ── Intentar OCR con cada máscara ─────────────────
                    string mejorResultado = "";

                    foreach (var (mascara, nombre) in mascaras)
                    {
                        if (mascara == null || mascara.IsDisposed) continue;

                        OCV.Mat clean = null;
                        try
                        {
                            clean = new OCV.Mat();
                            using var kClose = OCV.Cv2.GetStructuringElement(
                                OCV.MorphShapes.Rect, new OCV.Size(3, 5));
                            OCV.Cv2.MorphologyEx(mascara, clean, OCV.MorphTypes.Close, kClose);

                            double frac = (double)OCV.Cv2.CountNonZero(clean)
                                        / (clean.Width * clean.Height);

                            if (frac < 0.003 || frac > 0.65)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"[OCR {nombre}] descartado frac={frac:P1}");
                                continue;
                            }

                            if (_tesseract == null) continue;

                            string resultado = IntentarTesseract(clean, nombre);
                            System.Diagnostics.Debug.WriteLine(
                                $"[OCR {nombre}] frac={frac:P1} → '{resultado}'");

                            if (EsNumeroValido(resultado))
                            {
                                mejorResultado = resultado;
                                break;
                            }
                        }
                        finally { clean?.Dispose(); }
                    }

                    System.Diagnostics.Debug.WriteLine(
                        string.IsNullOrEmpty(mejorResultado)
                            ? "[OCR] Sin resultado válido"
                            : $"[OCR] FINAL: '{mejorResultado}'");

                    return mejorResultado;
                }
                finally
                {
                    // Liberar todas las máscaras
                    foreach (var (m, _) in mascaras)
                        try { m?.Dispose(); } catch { }

                    // Liberar Mat intermedios en orden inverso
                    gray?.Dispose();
                    roiX4?.Dispose();
                    roi?.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OCR Error] {ex.Message}");
                return "";
            }
        }

        private string IntentarTesseract(OCV.Mat binMat, string nombreDebug)
        {
            try
            {
                string resultado = OCRImagen(binMat, nombreDebug);

                // Si no detecta un número válido, intentar con imagen volteada
                if (!EsNumeroValido(resultado))
                {
                    using var flip = new OCV.Mat();
                    OCV.Cv2.Flip(binMat, flip, OCV.FlipMode.Y);

                    resultado = OCRImagen(flip, nombreDebug + "_flip");
                }

                return resultado;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Tesseract {nombreDebug}] {ex.Message}");
                return "";
            }
        }

        private string OCRImagen(OCV.Mat img, string nombreDebug)
        {
            try
            {
                using var bmp = MatToBitmap(img);

                lock (_tLock)
                {
                    _tesseract.SetVariable("tessedit_char_whitelist", "0123456789.");

                    foreach (var psm in new[] { PageSegMode.SingleLine, PageSegMode.SingleWord })
                    {
                        try
                        {
                            using var pix = PixConverter.ToPix(bmp);
                            using var page = _tesseract.Process(pix, psm);

                            string txt = page.GetText().Trim();

                            string limpio = System.Text.RegularExpressions.Regex
                                .Replace(txt, @"[^0-9.]", "");

                            if (!string.IsNullOrEmpty(limpio))
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"[Tesseract {nombreDebug} PSM{(int)psm}] → '{limpio}'");

                                return limpio;
                            }
                        }
                        catch { }
                    }

                    return "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Tesseract OCRImagen {nombreDebug}] {ex.Message}");
                return "";
            }
        }

        private static bool EsNumeroValido(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt)) return false;
            if (!double.TryParse(txt,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double v)) return false;
            return (v >= 15 && v <= 200) || (v >= 150 && v <= 2000);
        }

        // ════════════════════════════════════════════════════════════
        //  BUFFERS
        // ════════════════════════════════════════════════════════════

        private double PushVol(double v)
        {
            if (v < EGG_VOL_MIN || v > EGG_VOL_MAX)
                return _qVol.Count > 0 ? Mediana(_qVol.ToList()) : 0;
            _qVol.Enqueue(v);
            if (_qVol.Count > BUF_VOL) _qVol.Dequeue();
            return Mediana(_qVol.ToList());
        }

        private void PushPeso(string ocr)
        {
            if (string.IsNullOrWhiteSpace(ocr)) { _pesoOk = false; return; }
            string s = System.Text.RegularExpressions.Regex.Replace(ocr.Trim(), @"[^0-9.]", "");
            if (string.IsNullOrEmpty(s)) { _pesoOk = false; return; }

            if (!double.TryParse(s,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double p)) { _pesoOk = false; return; }

            if (!s.Contains('.'))
            {
                if (p >= 100 && p <= 9999) p /= 10.0;
                else if (p >= 1000 && p <= 99999) p /= 100.0;
            }

            if (p < 25 || p > 200) { _pesoOk = false; return; }

            _qPeso.Enqueue(p);
            if (_qPeso.Count > BUF_PESO) _qPeso.Dequeue();

            var lst = _qPeso.ToList();
            double cand = lst.Last();
            int iguales = lst.Count(x => Math.Abs(x - cand) <= TOL_PESO);

            if (iguales >= REP_MIN)
            {
                var sim = lst.Where(x => Math.Abs(x - cand) <= TOL_PESO).ToList();
                _pesoEstable = Math.Round(Mediana(sim), 1);
                _pesoOk = true;
            }
            else { _pesoOk = false; }
        }

        // ════════════════════════════════════════════════════════════
        //  DIBUJAR
        // ════════════════════════════════════════════════════════════

        private void DibujarFrame(
            System.Drawing.Bitmap frame,
            OCV.Point[] cont, OCV.RotatedRect? elipse, OCV.Rect rect,
            bool huevoOk, bool medOk,
            double pxCm, double dCm, double lCm,
            double volEst, double volBruto, int mm,
            string ocr, string modo, string diag)
        {
            using var g = System.Drawing.Graphics.FromImage(frame);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (huevoOk)
            {
                var colorContorno = medOk
                    ? System.Drawing.Color.LimeGreen
                    : System.Drawing.Color.OrangeRed;

                if (cont != null && cont.Length > 2)
                {
                    var pts = Array.ConvertAll(cont, p => new System.Drawing.PointF(p.X, p.Y));
                    using var pc = new System.Drawing.Pen(colorContorno, 2.5f);
                    g.DrawPolygon(pc, pts);
                }

                if (elipse.HasValue)
                {
                    var el = elipse.Value;
                    using var pe = new System.Drawing.Pen(System.Drawing.Color.Magenta, 2f);
                    g.DrawEllipse(pe,
                        el.Center.X - el.Size.Width / 2f,
                        el.Center.Y - el.Size.Height / 2f,
                        el.Size.Width, el.Size.Height);
                }

                if (medOk)
                {
                    using var pm = new System.Drawing.Pen(System.Drawing.Color.Yellow, 1.8f);
                    pm.CustomStartCap = new System.Drawing.Drawing2D.AdjustableArrowCap(4, 4);
                    pm.CustomEndCap = new System.Drawing.Drawing2D.AdjustableArrowCap(4, 4);
                    int cy = rect.Y + rect.Height / 2, cx = rect.X + rect.Width / 2;
                    g.DrawLine(pm, rect.Left, cy, rect.Right, cy);
                    g.DrawLine(pm, cx, rect.Top, cx, rect.Bottom);
                    using var fm = new System.Drawing.Font("Segoe UI", 8f, System.Drawing.FontStyle.Bold);
                    g.DrawString($"∅ {dCm:F2} cm", fm, System.Drawing.Brushes.Yellow, rect.Right + 4, cy - 9);
                    g.DrawString($"↕ {lCm:F2} cm", fm, System.Drawing.Brushes.Yellow, cx + 4, rect.Bottom + 2);
                }
                else
                {
                    using var fw = new System.Drawing.Font("Segoe UI", 8f, System.Drawing.FontStyle.Bold);
                    g.DrawString($"⚠ largo={lCm:F1}cm — escala dudosa ({pxCm:F0}px/cm)",
                        fw, System.Drawing.Brushes.OrangeRed, rect.X, Math.Max(0, rect.Y - 20));
                }
            }

            {
                int rx1 = (int)(frame.Width * ROI_X1);
                int ry1 = (int)(frame.Height * ROI_Y1);
                int rw = (int)(frame.Width * (ROI_X2 - ROI_X1));
                int rh = (int)(frame.Height * (ROI_Y2 - ROI_Y1));
                using var pr = new System.Drawing.Pen(System.Drawing.Color.Orange, 1.5f)
                { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
                g.DrawRectangle(pr, rx1, ry1, rw, rh);
                using var fr2 = new System.Drawing.Font("Segoe UI", 7f);
                g.DrawString("ROI Display", fr2, System.Drawing.Brushes.Orange, rx1 + 2, ry1 - 14);
            }

            {
                int ly = (int)(frame.Height * VISION_Y_MAX);
                using var pl = new System.Drawing.Pen(
                    System.Drawing.Color.FromArgb(100, System.Drawing.Color.DeepSkyBlue), 1.5f)
                { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawLine(pl, 0, ly, frame.Width, ly);
            }

            DibujarPanel(g, dCm, lCm, volEst, volBruto, mm, pxCm, modo, diag, huevoOk, medOk);

            if (!string.IsNullOrWhiteSpace(ocr))
            {
                using var fo = new System.Drawing.Font("Consolas", 9f);
                string lin = ocr.Replace("\n", " | ");
                if (lin.Length > 60) lin = lin.Substring(0, 60) + "…";
                var sz = g.MeasureString(lin, fo);
                int py = frame.Height - 30;
                g.FillRectangle(
                    new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(160, 0, 0, 0)),
                    5, py - 4, sz.Width + 8, sz.Height + 4);
                g.DrawString(lin, fo, System.Drawing.Brushes.White, 9, py);
            }
        }

        private void DibujarPanel(
            System.Drawing.Graphics g,
            double dCm, double lCm, double volEst, double volBruto,
            int mm, double pxCm, string modo, string diag,
            bool huevoOk, bool medOk)
        {
            int w = 270;
            int h = !huevoOk ? 58 : (medOk ? 220 : 140);
            var bgColor = System.Drawing.Color.FromArgb(180, 0, 0, 0);
            var brdColor = medOk ? System.Drawing.Color.LimeGreen
                         : huevoOk ? System.Drawing.Color.OrangeRed
                                   : System.Drawing.Color.Gray;

            g.FillRectangle(new System.Drawing.SolidBrush(bgColor), 8, 8, w, h);
            g.DrawRectangle(
                new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, brdColor), 1.2f),
                8, 8, w, h);

            using var ft = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold);
            using var fv = new System.Drawing.Font("Segoe UI", 8.5f);
            using var fi = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Italic);

            int y = 14;
            g.DrawString("🥚 Medición de Huevo", ft, System.Drawing.Brushes.LimeGreen, 14, y); y += 20;

            if (!huevoOk)
            {
                g.DrawString("Sin huevo detectado", fi, System.Drawing.Brushes.Orange, 14, y);
                return;
            }

            if (!medOk)
            {
                g.DrawString("⚠ Escala en corrección…", ft,
                    System.Drawing.Brushes.OrangeRed, 14, y); y += 18;
                Fila(g, fv, ref y, "Largo calculado:", $"{lCm:F2} cm", System.Drawing.Color.OrangeRed);
                Fila(g, fv, ref y, "Escala:", $"{pxCm:F1} px/cm — {modo}", System.Drawing.Color.Orange);
                Fila(g, fv, ref y, "Diagnóstico:", diag, System.Drawing.Color.Gray);
                g.DrawString("Ajusta la cámara o la hoja milimetrada.", fi,
                    System.Drawing.Brushes.Gray, 14, y);
                return;
            }

            Fila(g, fv, ref y, "Diámetro (∅):", $"{dCm:F2} cm", System.Drawing.Color.Cyan);
            Fila(g, fv, ref y, "Largo:", $"{lCm:F2} cm", System.Drawing.Color.Cyan);
            Fila(g, fv, ref y, "Volumen estable:", $"{volEst:F3} cm³", System.Drawing.Color.Yellow);
            Fila(g, fv, ref y, "Volumen bruto:", $"{volBruto:F3} cm³",
                System.Drawing.Color.FromArgb(170, 210, 210, 0));
            Fila(g, fv, ref y, "Longitud:", $"{mm} mm", System.Drawing.Color.LightGray);
            Fila(g, fv, ref y, "Escala:", $"{pxCm:F1} px/cm", System.Drawing.Color.LightGray);
            Fila(g, fv, ref y, "Método:", modo, System.Drawing.Color.LightGreen);

            y += 4;
            if (_pesoOk)
            {
                using var fw = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold);
                g.DrawString($"Peso: {_pesoEstable:F1} g  ✓", fw,
                    System.Drawing.Brushes.LimeGreen, 14, y);
            }
            else
            {
                g.DrawString("Peso: estabilizando…", fi,
                    new System.Drawing.SolidBrush(System.Drawing.Color.Orange), 14, y);
            }
        }

        private void Fila(System.Drawing.Graphics g, System.Drawing.Font f,
                          ref int y, string lbl, string val, System.Drawing.Color c)
        {
            g.DrawString(lbl, f, System.Drawing.Brushes.White, 14, y);
            g.DrawString(val, f, new System.Drawing.SolidBrush(c), 140, y);
            y += 18;
        }

        // ════════════════════════════════════════════════════════════
        //  UTILIDADES
        // ════════════════════════════════════════════════════════════

        private OCV.Mat CropTop(OCV.Mat mat)
        {
            int h = Math.Max(1, (int)(mat.Rows * VISION_Y_MAX));
            return mat[new OCV.Rect(0, 0, mat.Cols, Math.Min(h, mat.Rows))].Clone();
        }

        private static int Clamp(int v, int min, int max) =>
            v < min ? min : v > max ? max : v;

        private static double Mediana(List<double> l)
        {
            if (l == null || l.Count == 0) return 0;
            var s = l.OrderBy(x => x).ToList();
            int m = s.Count / 2;
            return s.Count % 2 == 0 ? (s[m - 1] + s[m]) / 2.0 : s[m];
        }

        // ════════════════════════════════════════════════════════════
        //  TESSERACT
        // ════════════════════════════════════════════════════════════

        private void InicializarTesseract()
        {
            _tesseract = null;
            string base_ = AppDomain.CurrentDomain.BaseDirectory;
            string[] rutas =
            {
                Path.Combine(base_, "tessdata"),
                Path.Combine(base_, "..", "..", "tessdata"),
                Path.Combine(base_, "..", "..", "..", "tessdata"),
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                    "Tesseract-OCR", "tessdata"),
                @"C:\Program Files\Tesseract-OCR\tessdata",
                @"C:\Tesseract-OCR\tessdata",
            };

            string[][] langs =
            {
                new[] { "letsgodigital" },
                new[] { "digits" },
                new[] { "eng" },
            };

            foreach (var r in rutas)
            {
                string abs = Path.GetFullPath(r);
                if (!Directory.Exists(abs)) continue;
                foreach (var ls in langs)
                {
                    if (!ls.All(l => File.Exists(Path.Combine(abs, l + ".traineddata")))) continue;
                    try
                    {
                        string ls2 = string.Join("+", ls);
                        var engine = new TesseractEngine(abs, ls2, EngineMode.LstmOnly);

                        engine.SetVariable("tessedit_char_whitelist", "0123456789.");
                        engine.SetVariable("classify_bln_numeric_mode", "1");
                        engine.SetVariable("tessedit_ocr_engine_mode", "1");
                        engine.SetVariable("load_system_dawg", "0");
                        engine.SetVariable("load_freq_dawg", "0");

                        _tesseract = engine;
                        System.Diagnostics.Debug.WriteLine($"[Tesseract] OK {abs} | {ls2}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Tesseract] fallo {ls[0]}: {ex.Message}");
                        _tesseract = null;
                    }
                }
            }
            System.Diagnostics.Debug.WriteLine("[Tesseract] No encontrado. OCR desactivado.");
        }

        // ════════════════════════════════════════════════════════════
        //  CONVERSIONES BITMAP ↔ MAT
        // ════════════════════════════════════════════════════════════

        private static OCV.Mat BitmapToMat(System.Drawing.Bitmap bmp)
        {
            var r = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(r,
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                int total = stride * bmp.Height;
                byte[] px = new byte[total];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, total);
                var mat = new OCV.Mat(bmp.Height, bmp.Width, OCV.MatType.CV_8UC3);
                System.Runtime.InteropServices.Marshal.Copy(px, 0, mat.Data, total);
                return mat;
            }
            finally { bmp.UnlockBits(data); }
        }

        private static System.Drawing.Bitmap MatToBitmap(OCV.Mat mat)
        {
            OCV.Mat src; bool d = false;
            if (mat.Channels() == 1)
            {
                src = new OCV.Mat();
                OCV.Cv2.CvtColor(mat, src, OCV.ColorConversionCodes.GRAY2BGR);
                d = true;
            }
            else src = mat;

            var bmp = new System.Drawing.Bitmap(src.Width, src.Height,
                           System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var data = bmp.LockBits(
                new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            try
            {
                int total = Math.Abs(data.Stride) * src.Height;
                byte[] px = new byte[total];
                System.Runtime.InteropServices.Marshal.Copy(src.Data, px, 0, total);
                System.Runtime.InteropServices.Marshal.Copy(px, 0, data.Scan0, total);
            }
            finally
            {
                bmp.UnlockBits(data);
                if (d) src.Dispose();
            }
            return bmp;
        }

        // ════════════════════════════════════════════════════════════
        //  DISPOSE
        // ════════════════════════════════════════════════════════════

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_tLock) { _tesseract?.Dispose(); _tesseract = null; }
        }
    }
}