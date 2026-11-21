// Namespace isolation permits installing the full official FMODUnity integration later.
// These names must match official FMOD Engine 2.02.35 native libraries.
namespace SpireFmod
{
    public partial class VERSION
    {
#if UNITY_IOS && !UNITY_EDITOR
        public const string dll="__Internal";
#else
        public const string dll="fmod";
#endif
    }
}
namespace SpireFmod.Studio
{
    public partial class STUDIO_VERSION
    {
#if UNITY_IOS && !UNITY_EDITOR
        public const string dll="__Internal";
#else
        public const string dll="fmodstudio";
#endif
    }
}
