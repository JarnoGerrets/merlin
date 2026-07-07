namespace Merlin.Backend.Configuration;

public sealed class VisionOptions
{
    public bool Enabled { get; set; } = true;
    public bool WarmOnStartup { get; set; }
    public string PythonExecutable { get; set; } = "python";
    public string WorkerScriptPath { get; set; } = Path.Combine("VisionScripts", "vision_worker.py");
    public string ModelAssetPath { get; set; } = Path.Combine("VisionModels", "hand_landmarker.task");
    public string PreferredCameraName { get; set; } = string.Empty;
    public string Backend { get; set; } = "Auto";
    public string CaptureProfile { get; set; } = "Auto";
    public int CameraIndex { get; set; }
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public int Fps { get; set; } = 30;
    public bool MirrorPreview { get; set; } = true;
    public bool DebugPreview { get; set; }
    public int EmitRateHz { get; set; } = 30;
    public int MaxHands { get; set; } = 2;
    public int PrimaryLostGraceMs { get; set; } = 600;
    public double PrimarySwitchDistanceThreshold { get; set; } = 0.18;
    public double PinchStartRatio { get; set; } = 0.25;
    public double PinchHoldRatio { get; set; } = 0.32;
    public double PinchReleaseRatio { get; set; } = 0.40;
    public int PinchDebounceMs { get; set; } = 150;
    public string PinchCalibrationPath { get; set; } = Path.Combine("Logs", "VisionCalibration", "pinch-calibration-latest.json");
    public string MotionRegionCalibrationPath { get; set; } = Path.Combine("Logs", "VisionCalibration", "motion-region-calibration-latest.json");
    public double PinchCalibrationLeadInSeconds { get; set; } = 1.0;
    public double PinchCalibrationOpenSeconds { get; set; } = 2.5;
    public double PinchCalibrationPinchedSeconds { get; set; } = 2.5;
    public double PinchCalibrationReleaseSeconds { get; set; } = 2.0;
    public double PinchCalibrationPhasePauseSeconds { get; set; } = 1.0;
    public double MotionRegionCalibrationLeadInSeconds { get; set; } = 1.0;
    public double MotionRegionCalibrationCornerSeconds { get; set; } = 2.0;
    public double MotionRegionCalibrationPhasePauseSeconds { get; set; } = 1.0;
    public double MotionRegionCalibrationPadding { get; set; } = 0.04;
    public double SmoothingAlpha { get; set; } = 0.25;
    public double PointerDeadzone { get; set; } = 0.003;
    public double PointerGainX { get; set; } = 1.0;
    public double PointerGainY { get; set; } = 1.0;
    public double ControlRegionLeft { get; set; }
    public double ControlRegionTop { get; set; }
    public double ControlRegionRight { get; set; } = 1.0;
    public double ControlRegionBottom { get; set; } = 1.0;
}
