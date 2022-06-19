using UnityEngine.XR;

public class ButtonDetector
{
    InputDevice device;
    InputFeatureUsage<bool> button;

    bool lastValue;

    public delegate void ButtonDetectorDelegate(ButtonDetector xrbd);
    public event ButtonDetectorDelegate ButtonDown;
    public event ButtonDetectorDelegate ButtonUp;

    public ButtonDetector(InputDevice dev, InputFeatureUsage<bool> btn)
    {
        device = dev;
        button = btn;
    }

    public void Detect()
    {
        if (device.TryGetFeatureValue(button, out bool value))
        {
            if (!lastValue && value) ButtonDown?.Invoke(this);
            else if (lastValue && !value) ButtonUp?.Invoke(this);
            
            lastValue = value;
        }
    }
}