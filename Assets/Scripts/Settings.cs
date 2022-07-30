using System;
using System.IO;
using YamlDotNet.Serialization;

namespace Mondeto
{

public class Settings
{
    static Settings instance = new Settings();
    public static Settings Instance
    {
        get => instance;
        private set => instance = value;
    }

    // Server settings
    public string IPAddressToListen = "0.0.0.0";
    public int PortToListen = 15902;
    public string TlsPrivateKeyFile = "Assets/StreamingAssets/testCert/test.key";
    public string TlsCertificateFile = "Assets/StreamingAssets/testCert/test.crt";

    // Client settings
    public string HostToConnect = "devel.luftmensch.info";
    public int PortToConnect = 15902;
    public bool SkipCertificateValidation = false;

    // Other settings
    public string AvatarPath = "Assets/StreamingAssets/default_avatar.vrm";
    public string MimeTypesPath = "Assets/StreamingAssets/config/mime.types";
    public string SceneFile = "Assets/StreamingAssets/scene.yml";

    public string TempDirectory = "." + Path.DirectorySeparatorChar + "temp_data";

    public int ObjectLogSize = 1000;

    private Settings()
    {
    }

    public void Dump(TextWriter writer)
    {
        var serialzier = new SerializerBuilder().Build();
        serialzier.Serialize(writer, this);
    }
    
    public static void Load(TextReader reader)
    {
        // Use custom object factory
        // See: https://github.com/aaubry/YamlDotNet/wiki/Serialization.Deserializer#withobjectfactoryiobjectfactory
        var deserializer = new DeserializerBuilder().WithObjectFactory(new SettingsObjectFactory()).Build();
        Instance = deserializer.Deserialize<Settings>(reader);
    }

    // For creating a Settings object whose constructor is private
    // See: https://github.com/aaubry/YamlDotNet/wiki/Serialization.Deserializer#withobjectfactoryiobjectfactory
    class SettingsObjectFactory : IObjectFactory
    {
        IObjectFactory fallback;

        public SettingsObjectFactory()
        {
            // DefaultObjectFactory is inside ObjectFactories namespace
            // See: https://github.com/aaubry/YamlDotNet/blob/2c0ae4c1cc2703347deb3e72b54370477ab3498a/YamlDotNet/Serialization/ObjectFactories/DefaultObjectFactory.cs#L31
            fallback = new YamlDotNet.Serialization.ObjectFactories.DefaultObjectFactory();
        }

        public object Create(Type type)
        {
            if (type == typeof(Settings))
            {
                return new Settings();
            }
            else
            {
                return fallback.Create(type);
            }
        }
    }
}

}   // end namespace