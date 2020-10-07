using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

public class SceneLoader
{
    SyncNode node;

    Dictionary<string, uint> namedObjects = new Dictionary<string, uint>();

    public SceneLoader(SyncNode node)
    {
        this.node = node;
    }

    public async Task Load(TextReader reader)
    {
        YamlDocument document = ReadYaml(reader);

        var root = YamlExpect<YamlMappingNode>(document.RootNode);
        var objectsKey = new YamlScalarNode("objects");
        if (!root.Children.ContainsKey(objectsKey)) ThrowError(root, "No entry called objects found");
        var objects = YamlExpect<YamlSequenceNode>(root.Children[objectsKey]);

        // Process objects
        foreach (YamlNode elem in objects)
        {
            await LoadObject(elem);
        }
    }

    YamlDocument ReadYaml(TextReader reader)
    {
        var stream = new YamlStream();
        stream.Load(reader);

        if (stream.Documents.Count == 0)
        {
            throw new Exception("No document was found inside YAML");
        }
        return stream.Documents[0];
    }

    YamlDocument ReadYaml(string text)
    {
        using (var reader = new StringReader(text))
        {
            return ReadYaml(reader);
        }
    }

    public async Task LoadObject(string text)
    {
        await LoadObject(ReadYaml(text).RootNode);
    }

    async Task LoadObject(YamlNode yamlNode)
    {
        YamlMappingNode objNode = YamlExpect<YamlMappingNode>(yamlNode);

        uint objId = await node.CreateObject();
        SyncObject obj = node.Objects[objId];

        // Process fields
        foreach (var pair in objNode)
        {
            var keyNode = YamlExpect<YamlScalarNode>(pair.Key);
            if (keyNode.Value == null) ThrowError(keyNode, "Invalid field name");
            if (keyNode.Value == "$name")
            {
                // the key "$name" is special
                string name = YamlExpect<YamlScalarNode>(pair.Value).Value;
                namedObjects[name] = obj.Id;
                continue;
            }

            Console.WriteLine(keyNode.Value);
            IValue val = YamlToValue(pair.Value);

            obj.SetField(keyNode.Value, val);
        }
    }

    public IValue YamlToValue(string yaml)
    {
        var stream = new YamlStream();
        using (var reader = new StringReader(yaml))
        {
            stream.Load(reader);
        }
        return YamlToValue(stream.Documents[0].RootNode);
    }

    private IValue YamlToValue(YamlNode yaml)
    {
        if (yaml.Tag == "!vec") // vector
        {
            float[] components = YamlToFloatArray(yaml, 3);
            return new Vec {
                X = components[0], Y = components[1], Z = components[2]
            };
        }
        else if (yaml.Tag == "!quat")   // quaternion
        {
            float[] components = YamlToFloatArray(yaml, 4);
            return new Quat {
                W = components[0], X = components[1], Y = components[2], Z = components[3]
            };
        }
        else if (yaml.Tag == "!euler")  // Euler angle is converted to a quaternion
        {
            float[] xyz = YamlToFloatArray(yaml, 3);
            xyz = xyz.Select(deg => (float)(deg * Math.PI / 180.0)).ToArray();   // deg to rad
            return Quat.FromEuler(xyz[0], xyz[1], xyz[2]);
        }
        else if (yaml.Tag == "!load_file")  // load local file and become a Blob handle
        {
            return YamlHandleLoadFile(yaml);
        }
        else if (yaml.Tag == "!ref")    // reference to a object
        {
            string name = YamlExpect<YamlScalarNode>(yaml).Value;
            // TODO: Because current SceneLoader is one-pass, only reference to above-defined objects is supported.
            return new ObjectRef { Id = namedObjects[name] };
        }
        else if (yaml is YamlScalarNode scalar)
        {
            // Currently, type for YAML scalar is determined using TryParse.
            // Therefore, it might be different from YAML spec https://yaml.org/spec/1.2/spec.html#id2805071 .
            if (scalar.Style != YamlDotNet.Core.ScalarStyle.Plain)
                return new Primitive<string> { Value = scalar.Value };
            else if (int.TryParse(scalar.Value, out var intValue))
                return new Primitive<int> { Value = intValue };
            else if (float.TryParse(scalar.Value, out var floatValue))
                return new Primitive<float> { Value = floatValue };
            else if (TryParseBool(scalar.Value, out var boolValue))
                return new Primitive<bool> { Value = boolValue };
            else
                return new Primitive<string> { Value = scalar.Value };
        }
        else if (yaml is YamlSequenceNode seq)
        {
            return new Sequence {
                Elements = seq.Children.Select(YamlToValue).ToList()
            };
        }
        else
        {
            ThrowError(yaml, "Invalid value");
            return null;    // dummy (because this line is not recognized as unreachable)
        }
    }

    bool TryParseBool(string str, out bool val)
    {
        // According to YAML spec 1.2 ( https://yaml.org/spec/1.2/spec.html ),
        // boolean values are "true" or "false".
        if (str == "true")
        {
            val = true;
            return true;
        }
        else if (str == "false")
        {
            val = false;
            return true;
        }
        else
        {
            val = default;
            return false;
        }
    }

    private float[] YamlToFloatArray(YamlNode yaml, int size)
    {
        var seq = YamlExpect<YamlSequenceNode>(yaml);
        if (seq.Count() != size)
        {
            ThrowError(yaml, $"Length should be {size}, not {seq.Count()}");
        }

        return seq.Children.Select(elem => {
            var scalar = YamlExpect<YamlScalarNode>(elem);
            float val = default;
            if (scalar != null && !float.TryParse(scalar.Value, out val))
                ThrowError(elem, "Invalid number");
            return val;
        }).ToArray();;
    }

    private BlobHandle YamlHandleLoadFile(YamlNode yaml)
    {
        // TODO: explicit mime type i.e. !load_file ["foo.jpg", "image/jpeg"]
        // TODO: guess mime type from extension
        string path = YamlExpect<YamlScalarNode>(yaml).Value;
        byte[] data = File.ReadAllBytes(path);

        // Currently, we use "application/octet-stream" as a sign of unknown type.
        //  https://www.iana.org/assignments/media-types/application/octet-stream
        //  https://wiki.developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types$revision/1589213
        Blob blob = new Blob(data, "application/octet-stream");
        BlobHandle handle = blob.GenerateHandle();
        node.WriteBlob(handle, blob);
        
        return handle;
    }

    private T YamlExpect<T>(YamlNode yaml) where T : YamlNode
    {
        T expected = yaml as T;
        if (expected == null)
        {
            ThrowError(
                yaml,
                $"YAML node type {typeof(T).Name} expected, not {yaml.GetType().Name}"
            );
        }
        return expected;
    }

    private void ThrowError(YamlNode yaml, string msg)
    {
        throw new SceneLoadingException(yaml.Start.Line, yaml.Start.Column, msg);
    }

    public static YamlNode ValueToYaml(IValue value)
    {
        switch (value)
        {
            case Primitive<int> intValue:
            {
                var node = new YamlScalarNode(intValue.Value.ToString());
                node.Style = YamlDotNet.Core.ScalarStyle.Plain;
                return node;
            }
            case Primitive<float> floatValue:
            {
                var node = new YamlScalarNode(floatValue.Value.ToString());
                node.Style = YamlDotNet.Core.ScalarStyle.Plain;
                return node;
            }
            case Primitive<bool> boolValue:
            {
                var node = new YamlScalarNode(boolValue.Value ? "true" : "false");
                node.Style = YamlDotNet.Core.ScalarStyle.Plain;
                return node;
            }
            case Primitive<string> stringValue:
                return new YamlScalarNode(stringValue.Value);
            case Vec vec:
            {
                var node = new YamlSequenceNode(
                    new YamlScalarNode(vec.X.ToString()),
                    new YamlScalarNode(vec.Y.ToString()),
                    new YamlScalarNode(vec.Z.ToString())
                );
                node.Tag = "!vec";
                return node;
            }
            case Quat quat:
            {
                var node = new YamlSequenceNode(
                    new YamlScalarNode(quat.W.ToString()),
                    new YamlScalarNode(quat.X.ToString()),
                    new YamlScalarNode(quat.Y.ToString()),
                    new YamlScalarNode(quat.Z.ToString())
                );
                node.Tag = "!quat";
                return node;
            }
            case Sequence seq:
                return new YamlSequenceNode(seq.Elements.Select(ValueToYaml));
            default:
                return new YamlScalarNode("FAILED"); // TODO: FIXME:
        }
    }

    public static string ValueToYamlString(IValue value)
    {
        var stream = new YamlStream(new YamlDocument(ValueToYaml(value)));
        // Generate yaml text
        //https://github.com/aaubry/YamlDotNet/blob/b3cf63744380a9ec031ef9cc2409c39e0c92c953/YamlDotNet/RepresentationModel/YamlStream.cs#L112
        using (var writer = new StringWriter())
        {
            stream.Save(writer);
            return writer.ToString();
        }
    }

    public class SceneLoadingException : Exception
    {
        public readonly int Line, Column;

        public SceneLoadingException(int line, int column, string message)
            : base($"Line {line}, column {column}: {message}")
        {
            Line = line;
            Column = column;
        }
    }
}