using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

public class SceneLoader
{
    SyncNode node;

    public SceneLoader(SyncNode node)
    {
        this.node = node;
    }

    public async Task Load(TextReader reader)
    {
        var stream = new YamlStream();
        stream.Load(reader);
        if (stream.Documents.Count == 0)
        {
            throw new Exception("No document was found inside YAML");
        }
        YamlDocument document = stream.Documents[0];

        var root = YamlExpect<YamlMappingNode>(document.RootNode);
        var objectsKey = new YamlScalarNode("objects");
        if (!root.Children.ContainsKey(objectsKey)) ThrowError(root, "No entry called objects found");
        var objects = YamlExpect<YamlSequenceNode>(root.Children[objectsKey]);

        // Process objects
        foreach (YamlNode elem in objects)
        {
            YamlMappingNode objNode = YamlExpect<YamlMappingNode>(elem);

            uint objId = await node.CreateObject();
            SyncObject obj = node.Objects[objId];

            // Process fields
            foreach (var pair in objNode)
            {
                var keyNode = YamlExpect<YamlScalarNode>(pair.Key);
                if (keyNode.Value == null) ThrowError(keyNode, "Invalid field name");
                Console.WriteLine(keyNode.Value);
                IValue val = YamlToValue(pair.Value);

                obj.SetField(keyNode.Value, val);
            }
        }
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