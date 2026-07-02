using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

using Haare.Client.Core;
using Haare.Client.Core.Singleton;
using Haare.Client.UI;
using Haare.Util.Logger;

public class HaareClient
{
    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.BeforeSceneLoad )]
    static async void Main() {
        
        LogHelper.Log(LogHelper.FRAMEWORK,"Start Haare Framework");
        await Task.Delay( 1 );
        await Processor.WaitForCreation();
        
        await Processor.Instance.Constructor(InitializePlugin, RegisterProcesses);
        
    }

    static async UniTask InitializePlugin() {
        // SDK 로드
        await UniTask.Delay( 0 );
    }
    

    static async UniTask RegisterProcesses()
    {
        LogHelper.LogTask(LogHelper.FRAMEWORK,"RegisterProcesses");
        
        // 씬 전역에서 사용하는 object
        // 프로젝트가 새 Input System만 사용하므로 StandaloneInputModule 대신 InputSystemUIInputModule 사용
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var eventSystemObj = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(eventSystemObj);
        }

        if (Object.FindObjectOfType<AudioListener>() == null)
        {
            var audioObj = new GameObject("AudioListener", typeof(AudioListener));
            Object.DontDestroyOnLoad(audioObj);
        }

        
        LogHelper.LogTask(LogHelper.FRAMEWORK,"RegisterProcesses -> end");
        await UniTask.CompletedTask;
    }
    
}
