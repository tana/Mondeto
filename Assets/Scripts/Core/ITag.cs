namespace Mondeto.Core
{

public interface ITag
{
    void Setup(SyncObject syncObject);
    void Cleanup(SyncObject syncObject);
}

} // end namespace