using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VInspector;

public class PanelCircularTimer : MonoBehaviour
{
    #region SerializeFields
    
    [Tab("Tween")]
    [SerializeField] private float buttonTweenDuration = 0.7f;
    
    [Tab("SerializeFields")]
    [SerializeField] private RectTransform playButtonRect;
    [SerializeField] private RectTransform pauseOrResumeButtonRect;
    [SerializeField] private RectTransform pauseOrResumeButtonOriginPos;
    [SerializeField] private RectTransform pauseOrResumeButtonEndPos;
    [SerializeField] private RectTransform cancelButtonRect;
    [SerializeField] private RectTransform cancelButtonOriginPos;
    [SerializeField] private RectTransform cancelButtonEndPos;
    
    [SerializeField] public CircularProgressBar circularProgressBar;
    
    [SerializeField] private RectTransform focusMinuteSetter;
    [SerializeField] private ScrollSystem focusMinuteScroll;
    [SerializeField] private RectTransform relaxMinuteSetter;
    [SerializeField] private ScrollSystem relaxMinuteScroll;

    [SerializeField] private RectTransform currentSessionTimerTextRect;
    
    [SerializeField] private TMP_Text sessionText;

    [SerializeField] private GameObject needTodoItemSelect;
    
    [Tab("TodoItemUI")] 
    [SerializeField] private GameObject todoItemUI;
    [SerializeField] private TMP_Text currentTodoItemName;
    [SerializeField] private TMP_Text currentTodoItemDescription;
    
    #endregion

    #region CachingReferences

    private CanvasGroup playButtonCanvasGroup;
    private CanvasGroup pauseButtonCanvasGroup;
    private CanvasGroup cancelButtonCanvasGroup;

    private TMP_Text currentSessionTimerText;

    private Button playButton;
    private Button pauseButton;
    private Button resumeButton;
    private Button cancelButton;
    private PauseOrResumeButton pauseOrResumeButton;
    
    // 트윈 재사용을 위한 Sequence
    private Sequence pauseButtonTween;
    private Sequence cancelButtonTween;
    
    #endregion
    
    private void Awake()
    {
        playButtonCanvasGroup = playButtonRect.GetComponent<CanvasGroup>();
        pauseButtonCanvasGroup = pauseOrResumeButtonRect.GetComponent<CanvasGroup>();
        cancelButtonCanvasGroup = cancelButtonRect.GetComponent<CanvasGroup>();

        currentSessionTimerText = currentSessionTimerTextRect.GetComponent<TMP_Text>();

        pauseOrResumeButton = pauseOrResumeButtonRect.GetComponentInChildren<PauseOrResumeButton>();
        
        playButton = playButtonRect.GetComponentInChildren<Button>();
        pauseButton = pauseOrResumeButton.pauseButton;
        resumeButton = pauseOrResumeButton.resumeButton;
        cancelButton = cancelButtonRect.GetComponentInChildren<Button>();
        
        // 일시정지버튼 - 확인 절차 거치기
        pauseButton.onClick.RemoveAllAndAddListener(() =>
        {
            PopupManager.Instance.ShowConfirmPopup(
                "일시정지 확인",
                "정말로 일시정지 하시겠습니까?",
                () =>
                {
                    // 타이머 일시정지
                    TimerManager.Instance.PauseTimer();

                    // 버튼 UI 변화
                    pauseOrResumeButton.ShowResumeButton();

                    // 원형 진행도 UI 색 변화
                    circularProgressBar.ChangeColorPaused();
                }
            );
        });

        resumeButton.onClick.RemoveAllAndAddListener(() =>
        {
            // 타이머 재개
            TimerManager.Instance.ResumeTimer();
            
            // 버튼 UI 변화
            pauseOrResumeButton.ShowPauseButton();
        });
        
        // 정지 버튼 - 확인 절차 거치기 
        cancelButton.onClick.RemoveAllAndAddListener(() =>
        {
            PopupManager.Instance.ShowConfirmPopup(
                "정지 확인",
                "정말로 정지 하시겠습니까?",
                () =>
                {
                    // 타이머 취소
                    TimerManager.Instance.CancelTimer();
            
                    // UI 변화
                    ResetTimerUI();
                }
            );
        });

        // TimerManager OnTimeUpdated 이벤트 연결
        TimerManager.Instance.OnTimeUpdated += UpdateTimeDisplay;
        
        // 타이머 상태 변경 시 호출될 메서드 등록
        TimerManager.Instance.OnTimerStateChanged += OnTimerStateChanged;  
        
        TimerManager.Instance.OnSessionCompleted.AddListener(WhenSessionCompleted);
    }
    
    private void OnEnable()
    {
        TrySetTodoItemUI();  // TodoItem UI 설정
        
        // 타이머 상태에 따라 초기화 또는 수복 절차를 진행
        DebugEx.Log($"OnEnable : CurrentTimerState {TimerManager.Instance.CurrentTimerState}");
        switch (TimerManager.Instance.CurrentTimerState)
        {
            case TimerState.NeedToBeInitialized:
                ShowFocusMinuteSetter();
                break;
            case TimerState.ReadyForFocusSession:      
                PrepareFocusSession(TimerManager.Instance.focusMinute);
                break;
            case TimerState.ReadyForRelaxSession:
                PrepareRelaxSession(TimerManager.Instance.relaxMinute);
                break;
            case TimerState.FocusSessionRunning or TimerState.RelaxSessionRunning:
                UpdateRunningUI(TimerManager.Instance.CurrentTimerState);
                break;
            case TimerState.Paused:
                UpdatePausedUI();
                break;
            case TimerState.Stopped:
                ShowFocusMinuteSetter();
                break;
            case TimerState.PpomodoroEnd:
                ShowFocusMinuteSetter();
                break;
        }
    }

    private void OnTimerStateChanged(TimerState newState)
    {
        Debug.Log($"{nameof(OnTimerStateChanged)} : CurrentTimerState {newState}");
        
        switch (newState)
        {
            case TimerState.NeedToBeInitialized:
                ShowFocusMinuteSetter();
                break;
            case TimerState.ReadyForFocusSession:
                PrepareFocusSession(TimerManager.Instance.focusMinute);
                break;
            case TimerState.ReadyForRelaxSession:
                PrepareRelaxSession(TimerManager.Instance.relaxMinute);
                break;
            case TimerState.FocusSessionRunning:
                if (TimerManager.Instance.focusMinute > TimerManager.Instance.CurrentTodoItem.GetRemainingTimeOfToday())
                    TimerManager.Instance.focusMinute = TimerManager.Instance.CurrentTodoItem.GetRemainingTimeOfToday();
                
                StartFocusSession(TimerManager.Instance.focusMinute);
                UpdateRunningUI(newState);
                break;
            case TimerState.RelaxSessionRunning:
                StartRelaxSession(TimerManager.Instance.relaxMinute);
                UpdateRunningUI(newState);
                break;
            case TimerState.Paused:
                UpdatePausedUI();
                break;
            case TimerState.Stopped:
                ShowFocusMinuteSetter();
                break;
            case TimerState.PpomodoroEnd:
                ShowFocusMinuteSetter();
                break;
        }
    }
    
    /// <summary>
    /// 일시정지된 상태의 UI를 업데이트합니다.
    /// </summary>
    private void UpdatePausedUI()
    {
        DebugEx.Log("<color='orange'>타이머 일시정지 상태로 UI 업데이트</color>");
        pauseOrResumeButton.ShowResumeButton();
        HidePlayButton();
        ShowPauseAndCancelButton();
        currentSessionTimerTextRect.gameObject.SetActive(true);
    }

    /// <summary>
    /// 실행 중인 상태의 UI를 업데이트.
    /// </summary>
    private void UpdateRunningUI(TimerState state)
    {
        DebugEx.Log("<color='red'>UpdateRunningUI : 타이머 실행 중 상태로 UI 업데이트</color>");
        pauseOrResumeButton.ShowPauseButton();

        if (state == TimerState.FocusSessionRunning)
        {
            circularProgressBar.SetTotalTime(TimerManager.Instance.focusMinute);
            circularProgressBar.ChangeColorFocus();
        }
        else if (state == TimerState.RelaxSessionRunning)
        {
            circularProgressBar.SetTotalTime(TimerManager.Instance.relaxMinute);
            circularProgressBar.ChangeColorRelax();
        }
        
        sessionText.gameObject.SetActive(true);
        currentSessionTimerTextRect.gameObject.SetActive(true);
        HidePlayButton();
        ShowPauseAndCancelButton();
    }
    
    /// <summary>
    /// UI에 보이는 TodoItem의 정보를 변경하는 기능.
    /// TimerManager에 연결된 TodoItem의 정보를 가져오는 것을 시도한다.
    /// </summary>
    /// <param name="todoItem"></param>
    public bool TrySetTodoItemUI()
    {
        if (TimerManager.Instance.TodoItemInitialized)
        {
            todoItemUI.SetActive(true);
            currentTodoItemName.text = TimerManager.Instance.CurrentTodoItem.Name;
            currentTodoItemDescription.text = TimerManager.Instance.CurrentTodoItem.Description;

            DebugEx.Log($"Find Current TodoItem! : {TimerManager.Instance.CurrentTodoItem}");
            
            return true;        // 받아오는 데 성공함
        }
        else
        {
            UnsetTodoItemUI();
            DebugEx.Log($"Can't Find Current TodoItem!");
            return false;       // 받아오는 데 실패함
        }
    }

    /// <summary>
    /// TodoItem UI 비활성화
    /// </summary>
    public void UnsetTodoItemUI()
    {
        todoItemUI.SetActive(false);
        currentTodoItemName.text = string.Empty;
        currentTodoItemDescription.text = string.Empty;
    }

    public void CloseAndOpenTodoListPopup()
    {
        PopupManager.Instance.HidePopup();
        PopupManager.Instance.ShowPopup<TodoListPopup>();
    }
    
    #region SettingsProcedures

    /// <summary>
    /// 집중 시간 설정 절차
    /// </summary>
    private async void ShowFocusMinuteSetter()
    {
        // TodoItem 연동이 안되어있다면 하는 대응
        if (!TimerManager.Instance.TodoItemInitialized)
        {
            needTodoItemSelect.SetActive(true);
            playButton.interactable = false;
            return;
        }

        needTodoItemSelect.SetActive(false);
        playButton.interactable = true;
        
        // 어지간한 UI는 다 꺼지도록
        sessionText.gameObject.SetActive(false);
        currentSessionTimerText.gameObject.SetActive(false);
        
        circularProgressBar.Fill();
        circularProgressBar.ChangeColorFocus();
        
        // focusMinuteSetter를 활성화하고
        // 크기를 줄여놓고 커지는 애니메이션 적용
        // 그리고 초기화시키기
        focusMinuteSetter.gameObject.SetActive(true);
        relaxMinuteSetter.gameObject.SetActive(false);
        focusMinuteSetter.localScale = Vector3.zero;
        
        // ScrollSystem 초기화
        // 집중 세션의 기본값은 25분
        focusMinuteScroll.Setup(0, 60, OnFocusMinuteChanged, 25);  
        
        await focusMinuteSetter.DOScale(Vector3.one, 0.5f).AsyncWaitForCompletion();

        // 레이아웃이 안정된 후 초기화 수행
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);  // 프레임 끝까지 기다림 (레이아웃 안정화)
        
        ShowPlayButton();
        HidePauseAndCancelButton();
        
        // 그리고 버튼의 기능 변경
        playButton.onClick.RemoveAllAndAddListener(() =>
        {
            // 이 함수가 실행될 때 focusMinuteSetter는 켜져있을 것을 상정한다.
            // 그러니까 focusMinuteSetter를 비활성화
            focusMinuteSetter.gameObject.SetActive(false);
            
            ShowRelaxMinuteSetter();
        });
    }
    
    /// <summary>
    /// 휴식 시간 설정 절차
    /// </summary>
    private async void ShowRelaxMinuteSetter()
    {
        focusMinuteSetter.gameObject.SetActive(false);
        
        relaxMinuteSetter.gameObject.SetActive(true); // relaxMinuteSetter 활성화
        relaxMinuteSetter.localScale = Vector3.zero;  // 이후 애니메이션 보이도록 미리 크기를 줄임
        
        // ScrollSystem 초기화
        // 휴식 세션의 기본값은 5분
        relaxMinuteScroll.Setup(0, 60, OnRelaxMinuteChanged, 5);
        
        await relaxMinuteSetter.DOScale(Vector3.one, 0.5f).AsyncWaitForCompletion();
        
        // 레이아웃이 안정될 때까지 대기
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);  // 프레임 끝까지 기다림 (레이아웃 안정화)

        // playButton 기능 변경
        playButton.onClick.RemoveAllAndAddListener(() =>
        {
            string message = $"집중시간 {TimerManager.Instance.focusMinute}분 \n" +
                             $"휴식시간 {TimerManager.Instance.relaxMinute}분 \n";
            
            // 사이클 계산해서 보여주기
            int remainingTimeOfToday = TimerManager.Instance.CurrentTodoItem.GetRemainingTimeOfToday(); // 남은 시간을 분 단위로 변환
            TimerManager.Instance.lastCycleTime = remainingTimeOfToday % TimerManager.Instance.focusMinute;
            TimerManager.Instance.remainingCycleCount = remainingTimeOfToday / TimerManager.Instance.focusMinute +
                                                        ((TimerManager.Instance.lastCycleTime > 0) ? 1 : 0);

            message += "\n<color=yellow><size=80%>";
            message += $"오늘 할 남은 시간: {remainingTimeOfToday}분\n";
            message += $"총 사이클: {TimerManager.Instance.remainingCycleCount}번\n";
            if (TimerManager.Instance.lastCycleTime > 0)
            {
                message += $"마지막 사이클: {TimerManager.Instance.lastCycleTime}분";
            }
            message += "</size></color>";
            
            if (TimerManager.Instance.relaxMinute == 0)
            {
                message += "\n\n<size=60%>휴식시간이 0으로 설정되었기에\n" +
                           "뽀모도로가 아닌 일반 타이머로 진행됩니다.</size>";
            }
            
            message += "\n\n<size=60%>확인을 누르시면 타이머를 시작합니다.</size>";
            
            PopupManager.Instance.ShowConfirmPopup("타이머 시작", message, async () =>
            {
                // 팝업의 확인 버튼에 들어갈 기능들
                
                // playButton 숨기고
                HidePlayButton();
                // 일시정지랑 취소 버튼 활성화
                ShowPauseAndCancelButton();

                // 타이머 텍스트 보이기
                currentSessionTimerTextRect.gameObject.SetActive(true);
                currentSessionTimerTextRect.localScale = Vector3.zero;
                
                await relaxMinuteSetter.DOScale(Vector3.zero, 0.5f).ToUniTask();
                
                relaxMinuteSetter.gameObject.SetActive(false);
                currentSessionTimerTextRect.DOScale(Vector3.one, 0.5f);
                
                // 첫번째 집중 세션 타이머 시작
                TimerManager.Instance.CurrentTimerState = TimerState.FocusSessionRunning;

                // playButton의 기능을 모두 지우고
                // (playButton 숨기기 & pauseButton 보이기 & cancelButton 보이기) 기능 연결
                playButton.onClick.RemoveAllAndAddListeners(HidePlayButton, ShowPauseAndCancelButton);
            });
        });
    }
    
    private void OnFocusMinuteChanged(int minute)
    {
        TimerManager.Instance.focusMinute = minute;
    }

    private void OnRelaxMinuteChanged(int minute)
    {
        TimerManager.Instance.relaxMinute = minute;
    }
    
    #endregion
    
    #region Timer Management
    
    /// <summary>
    /// 바로 시작할 수 있도록 집중 세션 준비
    /// </summary>
    public void PrepareFocusSession(int focusMinute)
    {
        sessionText.gameObject.SetActive(true);
        sessionText.text = "집중 세션 \n준비";
        
        TrySetTodoItemUI();  // TodoItem UI 설정
        
        // 원형 진행바 세팅
        circularProgressBar.SetTotalTime(focusMinute);
        circularProgressBar.Fill();
        circularProgressBar.ChangeColorFocus();
        
        // focusMinuteSetter 비활성화
        focusMinuteSetter.gameObject.SetActive(false);

        // 세션 시간 시각화
        TimeSpan time = TimeSpan.FromMinutes(focusMinute);
        currentSessionTimerText.text = $"{time.Minutes:D2}:00";
        
        // 타이머 시간 텍스트 애니메이션과 함께 보이기
        currentSessionTimerTextRect.gameObject.SetActive(true);
        currentSessionTimerTextRect.localScale = Vector3.zero;
        currentSessionTimerTextRect.DOScale(Vector3.one, 0.5f);

        //TimerManager.Instance.OnTimeUpdated -= UpdateTimeDisplay;
        //TimerManager.Instance.OnTimeUpdated += UpdateTimeDisplay;
        
        // playButton에 기능 다 빼고, 다음 세션 시작하는 기능만 넣기
        HidePauseAndCancelButton();
        ShowPlayButton();
        playButton.onClick.RemoveAllAndAddListener(() =>
        {
            TimerManager.Instance.CurrentTimerState = TimerState.FocusSessionRunning;
        });
    }

    /// <summary>
    /// 바로 시작할 수 있도록 휴식 세션 준비
    /// </summary>
    public void PrepareRelaxSession(int relaxMinute)
    {
        sessionText.gameObject.SetActive(true);
        sessionText.text = "휴식 세션 \n준비";
        
        TrySetTodoItemUI();  // TodoItem UI 설정
        
        // 원형 진행바 세팅
        circularProgressBar.SetTotalTime(relaxMinute);
        circularProgressBar.Fill();
        circularProgressBar.ChangeColorRelax();
        
        // focusMinuteSetter 비활성화
        focusMinuteSetter.gameObject.SetActive(false);
        
        // 세션 시간 시각화
        TimeSpan time = TimeSpan.FromMinutes(relaxMinute);
        currentSessionTimerText.text = $"{time.Minutes:D2}:00";
        
        // 타이머 시간 텍스트 애니메이션과 함께 보이기
        currentSessionTimerTextRect.gameObject.SetActive(true);
        currentSessionTimerTextRect.localScale = Vector3.zero;
        currentSessionTimerTextRect.DOScale(Vector3.one, 0.5f);
        
        // playButton에 기능 다 빼고, 다음 세션 시작하는 기능만 넣기
        HidePauseAndCancelButton();
        ShowPlayButton();
        playButton.onClick.RemoveAllAndAddListener(() =>
        {
            TimerManager.Instance.CurrentTimerState = TimerState.RelaxSessionRunning;
        });
    }
    
    /// <summary>
    /// 집중 세션 시작
    /// </summary>
    private void StartFocusSession(int durationInMinutes)
    {
        DebugEx.Log($"<color='Red'>Start Focus Session : Duration: {durationInMinutes} minutes</color>");

        sessionText.gameObject.SetActive(true);

        int focusDurationInSeconds = durationInMinutes * 60;
        TimerManager.Instance.StartTimer(focusDurationInSeconds, TimerState.FocusSessionRunning);
        circularProgressBar.ChangeColorFocus(); // 집중 세션 색상으로 변경

        HidePlayButton();               // playButton 숨기기
        ShowPauseAndCancelButton();     // pauseButton & cancelButton 보이기

        // 타이머 텍스트 애니메이션과 함께 보이기
        currentSessionTimerTextRect.gameObject.SetActive(true);
        currentSessionTimerTextRect.localScale = Vector3.zero;
        currentSessionTimerTextRect.DOScale(Vector3.one, 0.5f);
    }

    /// <summary>
    /// 휴식 세션 시작
    /// </summary>
    private void StartRelaxSession(int durationInMinutes)
    {
        DebugEx.Log($"<color='Green'>Start Relax Session : Duration: {durationInMinutes} minutes</color>");

        sessionText.gameObject.SetActive(true);

        int relaxDurationInSeconds = durationInMinutes * 60;
        TimerManager.Instance.StartTimer(relaxDurationInSeconds, TimerState.RelaxSessionRunning);
        circularProgressBar.Fill();
        circularProgressBar.ChangeColorRelax(); // 휴식 세션 색상으로 변경

        // 타이머 텍스트 애니메이션과 함께 보이기
        currentSessionTimerTextRect.gameObject.SetActive(true);
        currentSessionTimerTextRect.localScale = Vector3.zero;
        currentSessionTimerTextRect.DOScale(Vector3.one, 0.5f);
    }
    
    private void UpdateTimeDisplay(int remainingTimeInSeconds)
    {
        // TimerManager에서 시간 업데이트 이벤트를 받을 때 호출됨
        TimeSpan time = TimeSpan.FromSeconds(remainingTimeInSeconds);

        if (TimerManager.Instance.CurrentTimerState is TimerState.FocusSessionRunning or TimerState.RelaxSessionRunning)
        {
            sessionText.gameObject.SetActive(true);
            
            string _sessionText = string.Empty;
            if(TimerManager.Instance.CurrentTimerState == TimerState.FocusSessionRunning)
                _sessionText = "집중 세션" + 
                               "\n진행 중";
            else if(TimerManager.Instance.CurrentTimerState == TimerState.RelaxSessionRunning)
                _sessionText = "휴식 세션" + 
                               "\n진행 중";
            
            sessionText.text = _sessionText + string.Concat(Enumerable.Repeat(".", 4 - (remainingTimeInSeconds % 3 + 1)));
        }
        else
        {
            sessionText.gameObject.SetActive(false);
        }
        
        currentSessionTimerText.text = $"{time.Minutes:D2}:{time.Seconds:D2}";

        circularProgressBar.UpdateByRemainingTime(remainingTimeInSeconds);
    }

    private void WhenSessionCompleted()
    {
        //currentSessionTimerText.text = "00:00";
        sessionText.gameObject.SetActive(false);
    }
    
    private void ResetTimerUI()
    {
        // 타이머가 취소되거나 완료될 때 UI를 초기 상태로 재설정
        currentSessionTimerTextRect.gameObject.SetActive(false);

        ShowFocusMinuteSetter();
        
        ShowPlayButton();
        HidePauseAndCancelButton();
    }
    
    #endregion
    
    #region Button UI

    public void ShowPlayButton()
    {
        SetActiveRect(playButtonRect, playButtonCanvasGroup, true);
    }

    public void HidePlayButton()
    {
        SetActiveRect(playButtonRect, playButtonCanvasGroup, false);
    }

    public void ShowPauseAndCancelButton()
    {
        MoveAndFadeRect(pauseOrResumeButtonRect, pauseOrResumeButtonEndPos.localPosition, pauseButtonCanvasGroup, true, ref pauseButtonTween);
        MoveAndFadeRect(cancelButtonRect, cancelButtonEndPos.localPosition, cancelButtonCanvasGroup, true, ref cancelButtonTween);
    }

    public void HidePauseAndCancelButton()
    {
        MoveAndFadeRect(pauseOrResumeButtonRect, pauseOrResumeButtonOriginPos.localPosition, pauseButtonCanvasGroup, false, ref pauseButtonTween);
        MoveAndFadeRect(cancelButtonRect, cancelButtonOriginPos.localPosition, cancelButtonCanvasGroup, false, ref cancelButtonTween);
    }

    private void SetActiveRect(RectTransform rect, CanvasGroup canvasGroup, bool isActive)
    {
        rect.gameObject.SetActive(isActive);
        canvasGroup.interactable = isActive;
        canvasGroup.alpha = isActive ? 1 : 0;
    }

    private void MoveAndFadeRect(RectTransform rect, Vector3 targetPosition, CanvasGroup canvasGroup, bool show, ref Sequence currentTween)
    {
        // 기존 트윈이 있는 경우 종료 처리
        if (currentTween != null && currentTween.IsPlaying())
        {
            currentTween.Kill();
            currentTween = null;
        }

        if (show) rect.gameObject.SetActive(true);
        
        // 새로운 트윈 시퀀스 생성 (SetAutoKill(false)로 재사용 가능하게 설정)
        currentTween = DOTween.Sequence()
            .Append(rect.DOLocalMove(targetPosition, buttonTweenDuration))
            .Join(canvasGroup.DOFade(show ? 1 : 0, buttonTweenDuration))
            .OnComplete(() =>
            {
                if (!show) rect.gameObject.SetActive(false);
            })
            .SetAutoKill(false);

        // 인터랙션 설정
        canvasGroup.interactable = show;
    }

    #endregion
}
