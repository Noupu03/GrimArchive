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

        if (!Application.isPlaying) return;

        try
        {
            LogHelper.Log(LogHelper.FRAMEWORK,"Start Haare Framework");
            await Task.Delay( 1 );
            await Processor.WaitForCreation();
            
            if (Processor.Instance != null)
            {
                await Processor.Instance.Constructor(InitializePlugin, RegisterProcesses);
            }
            else
            {
                Debug.LogError("[HaareClient] Processor.Instance is null after WaitForCreation!");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[HaareClient] Main Exception: {ex}");
        }
    }

    static async UniTask InitializePlugin() {
        await UniTask.Delay( 0 );
    }

    static async UniTask RegisterProcesses()
    {
        LogHelper.LogTask(LogHelper.FRAMEWORK,"RegisterProcesses");
        
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
