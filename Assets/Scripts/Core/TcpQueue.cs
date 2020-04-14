using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

public class TcpQueue
{
    Queue<ITcpMessage> queue = new Queue<ITcpMessage>();
    TcpClient tcp;
    
    bool running = true;

    public int Available { get => queue.Count; }

    public TcpQueue(TcpClient tcp)
    {
        this.tcp = tcp;
    }

    public async Task RunAsync()
    {
        while (running)
        {
            ITcpMessage msg = await ProtocolUtil.ReadTcpMessageAsync(tcp.GetStream());
            lock (queue)
            {
                queue.Enqueue(msg);
            }
        }
    }

    public ITcpMessage Dequeue()
    {
        lock (queue)
        {
            return queue.Dequeue();
        }
    }

    public void Stop()
    {
        running = false;
    }
}