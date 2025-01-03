using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TodoSystem
{
    [DefaultExecutionOrder(-1)]
    public class TodoManager : MonoBehaviour
    {
        // 싱글톤 인스턴스
        private static TodoManager instance;

        // 싱글톤 인스턴스에 접근하기 위한 프로퍼티
        public static TodoManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<TodoManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("TodoManager");
                        instance = go.AddComponent<TodoManager>();
                    }
                }
                return instance;
            }
        }
        
        // TodoList
        public List<TodoItem> TodoList { get; private set; } = new List<TodoItem>();
        // 검색용 딕셔너리
        private Dictionary<string, TodoItem> todoDictionary = new Dictionary<string, TodoItem>();
        
        public event Action<TodoItem> OnTodoItemAdded;
        
        // TodoList 저장 경로
        private string _filePath;

        private void Awake()
        {
            // 싱글톤 인스턴스 설정
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject); // 씬 전환 시 오브젝트가 파괴되지 않도록 설정
            }
            else if (instance != this)
            {
                Destroy(gameObject); // 이미 인스턴스가 존재하면 중복 오브젝트를 파괴
                return;
            }

            // 파일 경로 설정 (예: Application.persistentDataPath는 영구 저장 경로)
            _filePath = Path.Combine(Application.persistentDataPath, "todoList.json");

            // TodoList 로드
            LoadOrCreateSampleTodoItems();
            
            // Dictionary 초기화
            InitializeTodoDictionary();
            
            // 오늘의 할 일 필터링
            List<TodoItem> todaysTasks = FilterTodoItemsForToday(TodoList, DateTime.Today);
            
            DebugEx.Log("불러온 Todo 항목들:");         // 불러온 Todo항목 출력
            foreach (var todo in TodoList)
            {
                DebugEx.Log(todo.ToString());
            }
            DebugEx.Log("오늘의 할 일:");              // 오늘의 Todo항목 출력
            foreach (var todo in todaysTasks)
            {
                DebugEx.Log(todo.ToString());
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveCurrentTodoList();
            }
        }

        /// <summary>
        /// Dictionary를 TodoList 기반으로 초기화.
        /// </summary>
        private void InitializeTodoDictionary()
        {
            todoDictionary.Clear();
            foreach (var todoItem in TodoList)
            {
                if (!todoDictionary.TryAdd(todoItem.Id, todoItem))
                {
                    DebugEx.LogWarning($"중복된 Id 발견: {todoItem.Id}");
                }
            }
        }
        
        /// <summary>
        /// Todo리스트 로드를 시도한다.
        /// 저장된 Todo리스트가 없으면 샘플 데이터를 만든다.
        /// </summary>
        private void LoadOrCreateSampleTodoItems()
        {
            // JSON 파일에서 불러오기
            List<TodoItem> loadedTodoList = LoadTodoListFromJson(_filePath);

            if (loadedTodoList.Count == 0)
            {
                DebugEx.LogWarning("파일이 비어있음. 샘플 데이터를 생성함");
                
                // 파일이 없거나 비어 있으면 샘플 데이터 생성
                TodoList = CreateSampleTodoItems();
                // JSON 파일로 저장
                SaveTodoListToJson(TodoList, _filePath);
            }
            else
            {
                TodoList = loadedTodoList;
            }
        }
        
        /// <summary>
        /// 샘플 Todo항목들을 생성.
        /// </summary>
        List<TodoItem> CreateSampleTodoItems()
        {
            DateTime today = DateTime.Today;
            
            TodoItem codingStudy = new TodoItem(
                id : Guid.NewGuid().ToString(),
                name: "코딩 공부",
                description: "매일 90분씩 코딩 공부하기",
                startDate: today.AddDays(-2),
                endDate: today.AddDays(4),
                type: ItemType.TimeBased,
                priority: 5,
                recurrence: Recurrence.Daily,
                status: Status.Pending,
                dailyTargetDurationInMinutes: 90
            );
            
            codingStudy.AddProgress(today.AddDays(-2), 90);
            codingStudy.AddProgress(today.AddDays(-1), 45);
            codingStudy.AddProgress(today, 89);
            
            TodoItem majorStudy = new TodoItem(
                id : Guid.NewGuid().ToString(),
                name: "전공 공부",
                description: "매일 120분씩 전공 공부하기",
                startDate: today.AddDays(-2),
                endDate: today.AddDays(4),
                type: ItemType.TimeBased,
                priority: 5,
                recurrence: Recurrence.Daily,
                status: Status.Pending,
                dailyTargetDurationInMinutes: 120
            );
            
            majorStudy.AddProgress(today.AddDays(-2), 120);
            majorStudy.AddProgress(today.AddDays(-1), 120);
            majorStudy.AddProgress(today, 117);
            
            TodoItem drinkWater = new TodoItem(
                id : Guid.NewGuid().ToString(),
                name: "물 마시기",
                description: "오늘 물 8잔 마시기",
                startDate: today,
                endDate: today,
                type: ItemType.CheckBased,
                priority: 3,
                recurrence: Recurrence.None,
                status: Status.Pending
            );

            drinkWater.MarkAsCompletedOnDate(today);
            
            TodoItem healthCheck = new TodoItem(
                id : Guid.NewGuid().ToString(),
                name: "건강 검진 받기",
                description: "건강 검진 센터 방문",
                startDate: today.AddDays(2),
                endDate: today.AddDays(2),
                type: ItemType.CheckBased,
                priority: 8,
                recurrence: Recurrence.None,
                status: Status.Pending
            );

            TodoItem yogaClass = new TodoItem(
                id : Guid.NewGuid().ToString(),
                name: "요가 수업",
                description: "매주 화요일과 목요일에 요가 수업 참석",
                startDate: today.AddDays(-2),
                endDate: today.AddMonths(1),
                type: ItemType.TimeBased,
                priority: 6,
                recurrence: Recurrence.Weekly,
                status: Status.Pending,
                dailyTargetDurationInMinutes: 60,
                recurrenceDays: new List<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Thursday }
            );
            
            return new List<TodoItem>
            {
                codingStudy,
                majorStudy,
                drinkWater,
                healthCheck,
                yogaClass
            };
        }

        /// <summary>
        /// 주어진 Id로 TodoItem을 검색하는 기능
        /// </summary>
        /// <param name="id">검색할 TodoItem의 Id</param>
        /// <returns>해당 Id를 가진 OnTodoItemLink, 없으면 null</returns>
        public TodoItem FindTodoItemById(string id)
        {
            if (todoDictionary.TryGetValue(id, out TodoItem todoItem))
            {
                return todoItem;
            }
        
            DebugEx.LogWarning($"Id {id}로 TodoItem을 찾을 수 없습니다.");
            return null;
        }
        
        #region Save&Load

        /// <summary>
        /// TodoList와 딕셔너리를 비웁니다.
        /// </summary>
        public void ClearTodoList()
        {
            TodoList.Clear();
            todoDictionary.Clear();
        }
        
        /// <summary>
        /// TodoList를 JSON 파일로 저장.
        /// </summary>
        void SaveTodoListToJson(List<TodoItem> todoList, string filePath)
        {
            TodoListWrapper wrapper = new TodoListWrapper(todoList);

            string jsonString = JsonUtility.ToJson(wrapper, prettyPrint: true);
            File.WriteAllText(filePath, jsonString);

            DebugEx.Log($"Todo 리스트가 JSON 파일로 저장되었습니다: {filePath}");
        }

        /// <summary>
        /// JSON 파일에서 TodoList를 로드.
        /// </summary>
        List<TodoItem> LoadTodoListFromJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                DebugEx.LogWarning($"파일이 존재하지 않습니다: {filePath}");
                return new List<TodoItem>();
            }

            string jsonString = File.ReadAllText(filePath);
            TodoListWrapper wrapper = JsonUtility.FromJson<TodoListWrapper>(jsonString);

            if (wrapper != null && wrapper.TodoItems != null)
            {
                DebugEx.Log($"Todo 리스트가 JSON 파일에서 불러와졌습니다: {filePath}");

                // 로드한 후 내부 데이터 구조 초기화
                foreach (var todo in wrapper.TodoItems)
                {
                    todo.InitializeInternalDataStructures();
                }

                return wrapper.TodoItems;
            }
            else
            {
                DebugEx.LogWarning("Todo 리스트를 불러오는 데 실패했습니다.");
                return new List<TodoItem>();
            }
        }

        public void SaveCurrentTodoList()
        {
            SaveTodoListToJson(TodoList, _filePath);
        }

        public void LoadTodoList()
        {
            LoadOrCreateSampleTodoItems();
        }
        
        #endregion

        /// <summary>
        /// 특정 날짜에 해당하는 OnTodoItemLink 필터링.
        /// </summary>
        public List<TodoItem> FilterTodoItemsForToday(List<TodoItem> todoList, DateTime today)
        {
            List<TodoItem> filteredList = new List<TodoItem>();

            foreach (var todo in todoList)
            {
                if (todo.IsTaskScheduledForDate(today))
                {
                    filteredList.Add(todo);
                }
            }

            return filteredList;
        }
        
        #region Add&Delete

        /// <summary>
        /// 새로운 TodoItem을 추가하고 저장합니다.
        /// </summary>
        public void AddTodoItem(TodoItem todoItem)
        {
            if (!todoDictionary.ContainsKey(todoItem.Id))
            {
                TodoList.Add(todoItem);
                todoDictionary[todoItem.Id] = todoItem;
                SaveTodoListToJson(TodoList, _filePath);

                DebugEx.Log($"새로운 Todo 항목이 생겼습니다. {todoItem}");
                OnTodoItemAdded?.Invoke(todoItem);
            }
            else
            {
                DebugEx.LogWarning($"중복된 Id로 TodoItem을 추가할 수 없습니다: {todoItem.Id}");
            }
        }

        /// <summary>
        /// TodoItem을 삭제하고 저장합니다.
        /// </summary>
        public void DeleteTodoItem(TodoItem todoItem)
        {
            if (todoDictionary.ContainsKey(todoItem.Id))
            {
                TodoList.Remove(todoItem);
                todoDictionary.Remove(todoItem.Id);
                SaveTodoListToJson(TodoList, _filePath);

                DebugEx.Log($"{todoItem.Name} 할 일이 삭제되었습니다.");
            }
            else
            {
                DebugEx.LogWarning("삭제하려는 TodoItem을 찾을 수 없습니다.");
            }
        }

        #endregion
    }
    
    /// <summary>
    /// Todo항목의 종류를 정의하는 enum.
    /// </summary>
    [Serializable]
    public enum ItemType
    {
        TimeBased,  // 시간형
        CheckBased  // 확인형
    }

    /// <summary>
    /// Todo항목의 반복 여부를 정의하는 enum.
    /// </summary>
    [Serializable]
    public enum Recurrence
    {
        None,   // 반복 없음
        Daily,  // 매일 반복
        Weekly  // 매주 반복
    }

    /// <summary>
    /// Todo항목의 상태를 정의하는 enum.
    /// </summary>
    [Serializable]
    public enum Status
    {
        Pending,    // 대기 중
        InProgress, // 진행 중
        Completed   // 완료됨
    }
}
