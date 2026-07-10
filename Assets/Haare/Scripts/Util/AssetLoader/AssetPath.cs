using System.Collections.Generic;
using System;
using Demo.UI;

namespace Haare.Util.Loader
{
    public class AssetPath
    {

        public static Dictionary<long, string> ItemSOdict = 
        new Dictionary<long, string>
        {
            { 10001,"10001" }
        };
        
        // 원래 Demo 씬 3개("Assets/Haare/Demo/Scene/") 기준으로 하드코딩돼 있었다. 이 프로젝트는
        // SceneService를 아직 실제 씬 전환에는 안 쓰지만(Haare_프레임워크_정리.txt 6장 참고), 나중에
        // 씬을 추가할 때 바로 쓸 수 있도록 이 프로젝트의 실제 씬 폴더로 옮겨놓는다 — Demo 씬은 이
        // 경로로는 더 이상 SceneService.LoadScene()으로 못 불러오지만(참고용 코드일 뿐 실행 안 함),
        // ssh.unity 등 이 프로젝트 씬은 SceneName enum에 파일명과 같은 이름만 추가하면 바로 동작한다.
        public static string SCENE_PATH = "Assets/Scenes/";
        public static string SCENE_EXT = ".unity";

    }
}