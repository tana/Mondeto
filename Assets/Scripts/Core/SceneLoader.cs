using System;
using System.IO;
using YamlDotNet.RepresentationModel;

public class SceneLoader
{
    YamlDocument document;

    public SceneLoader(TextReader reader)
    {
        YamlStream stream = new YamlStream();
        stream.Load(reader);
        if (stream.Documents.Count == 0)
        {
            throw new Exception("No document was found inside YAML");
        }
        document = stream.Documents[0];
    }

    public void Load(SyncNode node)
    {
        throw new NotImplementedException();    // TODO
    }

    private IValue YamlToValue(YamlNode node)
    {
        throw new NotImplementedException();    // TODO
    }
}