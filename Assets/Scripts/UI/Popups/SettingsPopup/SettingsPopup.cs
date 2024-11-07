using System.IO;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPopup : Popup
{
    [SerializeField] private Transform panel;
    
    private void OnEnable()
    {
        panel.localScale = Vector3.zero;
        panel.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
    }

    // TODO 둠스데이 기능 연결할 버튼 만들기
    // TODO 구글 로그인 버튼도 만들기
    public async void DeleteAllData()
    {
        // 각 매니저의 캐시된 데이터 초기화
        ResetInventoryManager();
        ResetEconomyManager();
        ResetTodoManager();
        ResetPlaceableManager();
        
        // 데이터가 저장된 경로 가져오기
        string dataPath = Application.persistentDataPath;

        // 삭제할 데이터 파일 목록
        string[] dataFiles = new string[]
        {
            Path.Combine(dataPath, "inventoryData.json"),
            Path.Combine(dataPath, "currencyData.json"),
            Path.Combine(dataPath, "todoList.json"),
            Path.Combine(dataPath, "placeables.json")
        };

        // 각 파일 삭제
        foreach (string filePath in dataFiles)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                DebugEx.Log($"파일 삭제됨: {filePath}");
            }
            else
            {
                DebugEx.Log($"파일을 찾을 수 없음: {filePath}");
            }
        }

        // PlayerPrefs 데이터 초기화
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        DebugEx.Log("모든 PlayerPrefs 데이터가 초기화되었습니다.");

        DebugEx.Log("모든 매니저의 데이터가 초기화되었습니다.");

        // 매니저들이 데이터를 다시 로드하도록 합니다.
        await LoadAllManagers();

        DebugEx.Log("모든 매니저의 데이터가 다시 로드되었습니다.");
    }

    private static void ResetInventoryManager()
    {
        if (PomodoroHills.InventoryManager.Instance != null)
        {
            PomodoroHills.InventoryManager.Instance.ClearCachedItems();
            DebugEx.Log("InventoryManager의 캐시된 아이템이 초기화되었습니다.");
        }
    }

    private static void ResetEconomyManager()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.ResetCurrency();
            DebugEx.Log("EconomyManager의 재화가 초기화되었습니다.");
        }
    }

    private static void ResetTodoManager()
    {
        if (TodoSystem.TodoManager.Instance != null)
        {
            TodoSystem.TodoManager.Instance.ClearTodoList();
            DebugEx.Log("TodoManager의 TodoList가 초기화되었습니다.");
        }
    }

    private static void ResetPlaceableManager()
    {
        if (PlaceableManager.Instance != null)
        {
            PlaceableManager.Instance.ClearPlaceables();
            DebugEx.Log("PlaceableManager의 배치된 오브젝트가 초기화되었습니다.");
        }
    }

    private static async UniTask LoadAllManagers()
    {
        // 각 매니저의 데이터를 로드하여, 데이터가 없을 경우 샘플 데이터를 생성하도록 합니다.
        if (PomodoroHills.InventoryManager.Instance != null)
        {
            await PomodoroHills.InventoryManager.Instance.LoadItemsAsync();
            DebugEx.Log("InventoryManager의 데이터가 로드되었습니다.");
        }

        if (EconomyManager.Instance != null)
        {
            await EconomyManager.Instance.LoadCurrencyAsync();
            DebugEx.Log("EconomyManager의 데이터가 로드되었습니다.");
        }

        if (TodoSystem.TodoManager.Instance != null)
        {
            TodoSystem.TodoManager.Instance.LoadTodoList();
            DebugEx.Log("TodoManager의 데이터가 로드되었습니다.");
        }

        if (PlaceableManager.Instance != null)
        {
            PlaceableManager.Instance.LoadPlaceables();
            DebugEx.Log("PlaceableManager의 데이터가 로드되었습니다.");
        }
    }


    public void Close()
    {
        PopupManager.Instance.HidePopup();
    }
}