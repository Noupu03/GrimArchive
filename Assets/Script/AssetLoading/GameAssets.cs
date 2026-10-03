using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Haare.Util.Logger;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// 게임 에셋 로딩의 단일 진입점(2026-10-02 Resources → Addressables 전환). 모든 로드는 비동기다 —
// Resources.Load나 WaitForCompletion 같은 블로킹 로드는 쓰지 않는다. 매 틱·스폰 순간처럼 await할 수 없는
// 곳에서 읽어야 하는 에셋은 그 에셋을 쓰는 시스템이 async 초기화에서 미리 await해 필드에 들고 있는다
// (GameSession.Initialize 참고). 주소 문자열은 AssetKeys에 모아 둔다.
//
// 같은 주소·타입은 한 번만 로드하고 핸들을 게임 수명 동안 유지한다(Resources.Load로 불러 둔 에셋과 같은
// 수명). 한 번만 읽고 버리는 큰 에셋(맵 json)만 Release로 직접 놓는다.
public static class GameAssets
{
    private static readonly Dictionary<(string key, Type type), object> _handles = new();
    private static readonly Dictionary<(string label, Type type), object> _labelHandles = new();

    // 도메인 리로드 없이 Play를 다시 시작해도 이전 Play의 핸들(이미 무효)을 쓰지 않게 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        _handles.Clear();
        _labelHandles.Clear();
    }

    // warnIfMissing=false는 "없으면 기본값으로 폴백"하는 선택 에셋용(AI 설정 등) — 주소가 등록 안 돼 있어도
    // 경고 없이 null을 돌려준다.
    public static async UniTask<T> LoadAsync<T>(string key, bool warnIfMissing = true) where T : class
    {
        var cacheKey = (key, typeof(T));
        if (!_handles.TryGetValue(cacheKey, out object boxed))
        {
            // 등록 안 된 주소로 바로 LoadAssetAsync를 부르면 InvalidKeyException이 에러 로그로 찍힌다 —
            // 위치를 먼저 조회해 없으면 조용히(또는 경고 한 줄로) null을 돌려준다.
            if (!await HasLocationAsync(key))
            {
                if (warnIfMissing)
                    LogHelper.Warning(LogHelper.ASSETLOADER, $"GameAssets: 주소 '{key}'가 Addressables 그룹에 등록돼 있지 않습니다.");
                return null;
            }
            // 위 await 사이에 다른 호출이 같은 로드를 먼저 시작했을 수 있다 — 그 핸들을 같이 기다린다.
            if (!_handles.TryGetValue(cacheKey, out boxed))
            {
                boxed = Addressables.LoadAssetAsync<T>(key);
                _handles[cacheKey] = boxed;
            }
        }

        var handle = (AsyncOperationHandle<T>)boxed;
        try
        {
            // 이미 끝난 핸들이면 동기로 바로 결과가 나온다(두 번째 호출부터는 프레임 지연 없음).
            return await handle.ToUniTask();
        }
        catch (Exception e)
        {
            LogHelper.Error(LogHelper.ASSETLOADER, $"GameAssets: '{key}'({typeof(T).Name}) 로드 실패 — {e.Message}");
            if (_handles.Remove(cacheKey) && handle.IsValid()) Addressables.Release(handle);
            return null;
        }
    }

    // 스프라이트는 텍스처 주소에서 서브에셋 목록(IList<Sprite>)으로 받아 고른다 — Sprite Mode가 Multiple인
    // 텍스처(한 장만 잘린 obj/*.png 대부분이 그렇다)는 Sprite 타입으로 바로 로드하면 번들 빌드에서 이름이
    // 안 맞아 못 찾는다. spriteName이 없으면 첫 장(Resources.Load<Sprite>와 같은 결과)을 돌려준다.
    public static async UniTask<Sprite> LoadSpriteAsync(string key, string spriteName = null)
    {
        IList<Sprite> sprites = await LoadAsync<IList<Sprite>>(key);
        if (sprites == null || sprites.Count == 0) return null;
        if (string.IsNullOrEmpty(spriteName)) return sprites[0];

        foreach (var sprite in sprites)
            if (sprite != null && sprite.name == spriteName) return sprite;

        LogHelper.Warning(LogHelper.ASSETLOADER, $"GameAssets: '{key}'에 '{spriteName}' 스프라이트가 없습니다.");
        return null;
    }

    // 라벨이 붙은 에셋 전부(예: 유닛 프리팹 "Units").
    public static async UniTask<IList<T>> LoadByLabelAsync<T>(string label) where T : class
    {
        var cacheKey = (label, typeof(T));
        if (!_labelHandles.TryGetValue(cacheKey, out object boxed))
        {
            if (!await HasLocationAsync(label))
            {
                LogHelper.Warning(LogHelper.ASSETLOADER, $"GameAssets: 라벨 '{label}'이 붙은 Addressables 에셋이 없습니다.");
                return null;
            }
            if (!_labelHandles.TryGetValue(cacheKey, out boxed))
            {
                boxed = Addressables.LoadAssetsAsync<T>(label);
                _labelHandles[cacheKey] = boxed;
            }
        }

        var handle = (AsyncOperationHandle<IList<T>>)boxed;
        try
        {
            return await handle.ToUniTask();
        }
        catch (Exception e)
        {
            LogHelper.Error(LogHelper.ASSETLOADER, $"GameAssets: 라벨 '{label}'({typeof(T).Name}) 로드 실패 — {e.Message}");
            if (_labelHandles.Remove(cacheKey) && handle.IsValid()) Addressables.Release(handle);
            return null;
        }
    }

    // 한 번 읽고 버리는 큰 에셋을 메모리에서 내린다. 다시 LoadAsync하면 새로 로드한다.
    public static void Release<T>(string key) where T : class
    {
        var cacheKey = (key, typeof(T));
        if (!_handles.TryGetValue(cacheKey, out object boxed)) return;
        _handles.Remove(cacheKey);
        var handle = (AsyncOperationHandle<T>)boxed;
        if (handle.IsValid()) Addressables.Release(handle);
    }

    private static async UniTask<bool> HasLocationAsync(object key)
    {
        var handle = Addressables.LoadResourceLocationsAsync(key);
        try
        {
            IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation> locations = await handle.ToUniTask();
            return locations != null && locations.Count > 0;
        }
        catch (Exception e)
        {
            LogHelper.Error(LogHelper.ASSETLOADER, $"GameAssets: '{key}' 위치 조회 실패 — {e.Message}");
            return false;
        }
        finally
        {
            if (handle.IsValid()) Addressables.Release(handle);
        }
    }
}
