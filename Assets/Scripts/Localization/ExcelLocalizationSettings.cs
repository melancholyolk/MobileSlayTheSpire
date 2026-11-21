using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SpireLocalization
{
    // All locales and strings are already loaded synchronously from Resources.
    // The stock initializer unconditionally loads an Addressables catalog even with a custom table provider.
    // Override its documented virtual entry point so offline Excel assets need no Addressables content build.
    public sealed class ExcelLocalizationSettings : LocalizationSettings
    {
        AsyncOperationHandle<LocalizationSettings> ready;
        public override AsyncOperationHandle<LocalizationSettings> GetInitializationOperation()
        {
            if(!ready.IsValid()) ready=Addressables.ResourceManager.CreateCompletedOperation<LocalizationSettings>(this,null);
            return ready;
        }
        void OnDestroy()
        {
            if(ready.IsValid()) Addressables.Release(ready);
        }
    }
}
