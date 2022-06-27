using System;
using System.IO.Ports;
using System.Threading.Tasks;
using Mondeto.Core;

namespace Mondeto
{

public class LedButtonTag : ITag
{
    SyncObject obj;
    bool state = false;

    public void Setup(SyncObject syncObject)
    {
        syncObject.RegisterEventHandler("clickStart", OnClickStart);
        obj = syncObject;
        ChangeLedState(state);
    }

    public void OnClickStart(uint sender, IValue[] args)
    {
        state = !state;
        ChangeLedState(state);
    }

    async void ChangeLedState(bool isOn)
    {
        try
        {
            await Task.Run(() => {
                using (var port = new SerialPort("COM3", 115200))
                {
                    port.Open();
                    port.Write(isOn ? "l" : "h");
                }
            });
        }
        catch (Exception e)
        {
            obj.WriteErrorLog("LedButtonTag", e.ToString());
        }
    }

    public void Cleanup(SyncObject syncObject)
    {
        syncObject.DeleteEventHandler("clickStart", OnClickStart);
    }
}

}   // end namespace