using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [SerializeField] private List<GameObject> stages = new();

    [Tooltip("스테이지별 추가 오프셋 (자동 배치 위치에 더해짐)")]
    [SerializeField] private List<Vector2> stageOffsets = new();

    private readonly List<GameObject> _instances = new();
    private Vector3[] _stagePositions;
    private bool[] _cleared;

    private int _currentIndex = 0;
    public int CurrentIndex => _currentIndex;

    private bool _isTransitioning;

    /// <summary>스테이지 클리어 시 발생 (클리어된 스테이지 인덱스 전달)</summary>
    public static event Action<int> OnStageClear;

    private void OnEnable() => Stage.OnPlayerEnteredStage += HandlePlayerEnteredStage;
    private void OnDisable() => Stage.OnPlayerEnteredStage -= HandlePlayerEnteredStage;

    private void HandlePlayerEnteredStage(Stage stage)
    {
        if (stage.Index == _currentIndex) return;
        if (_isTransitioning) return;

        if (stage.UseFadeTransition && UIManager.Instance != null)
        {
            _isTransitioning = true;
            UIManager.Instance.FadeInOut(
                stage.FadeDuration, stage.FadeHoldDuration, stage.FadeDuration,
                () => ApplyStageTransition(stage),
                () => _isTransitioning = false,
                color: stage.FadeColor
            );
        }
        else
        {
            ApplyStageTransition(stage);
        }
    }

    private void ApplyStageTransition(Stage stage)
    {
        Time.timeScale = 1f;
        SetCurrentStage(stage.Index);
        stage.Activate();

        Player player = Player.Instance;
        if (player != null)
        {
            player.ResetOnStageTransition();
            player.Setup(GetStageEntryPoint(stage.Index));
            UIManager.Instance?.UpdateHealth(true);
        }
        if (stage.VirtualCamera != null)
            stage.VirtualCamera.PreviousStateIsValid = false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (stages.Count == 0) return;

        _stagePositions = new Vector3[stages.Count];
        _cleared = new bool[stages.Count];

        InstantiateAndPositionStages();
        UpdateActiveStages();
        RegisterCamerasToManager();
        SetupPlayerAtStart();
    }

    // 모든 프리팹을 원점에 생성 후 bounds 기반으로 순차 배치
    // Y는 이전 스테이지 afterClearSavePoint ↔ 다음 스테이지 beforeClearSavePoint가 같은 높이가 되도록 계산
    private void InstantiateAndPositionStages()
    {
        for (int i = 0; i < stages.Count; i++)
            _instances.Add(Instantiate(stages[i], Vector3.zero, Quaternion.identity));

        float nextX = 0f, nextY = 0f;
        for (int i = 0; i < _instances.Count; i++)
        {
            Bounds bounds = GetStageBounds(_instances[i]);
            float leftOffset = bounds.min.x - _instances[i].transform.position.x;

            Vector3 pos = _instances[i].transform.position;
            pos.x = nextX - leftOffset;
            pos.y = nextY;
            if (i < stageOffsets.Count)
                pos += (Vector3)stageOffsets[i];

            _instances[i].transform.position = pos;
            _stagePositions[i] = pos;
            nextX += bounds.size.x;

            if (!_instances[i].TryGetComponent(out Stage stage)) continue;
            stage.Init(i);
            nextY = ComputeNextStageY(stage, i);
        }
    }

    // 다음 스테이지 Y: 현재 afterClearSavePoint 세계 Y = 다음 beforeClearSavePoint 세계 Y
    // instances[i+1]은 아직 원점에 있으므로 position.y = 스테이지 루트 기준 로컬 Y
    private float ComputeNextStageY(Stage stage, int i)
    {
        if (i + 1 < _instances.Count
            && stage.AfterClearSavePoint != null
            && _instances[i + 1].TryGetComponent(out Stage nextStage)
            && nextStage.BeforeClearSavePoint != null)
        {
            return stage.AfterClearSavePoint.position.y - nextStage.BeforeClearSavePoint.position.y;
        }
        return 0f;
    }

    private void RegisterCamerasToManager()
    {
        if (CameraManager.Instance == null) return;

        var vcams = new List<CinemachineCamera>();
        foreach (var inst in _instances)
            vcams.Add(inst.TryGetComponent(out Stage s) ? s.VirtualCamera : null);
        //CameraManager.Instance.RegisterCameras(vcams);
    }

    private void SetupPlayerAtStart()
    {
        Player player = Player.Instance;
        if (player == null) return;

        player.Setup(GetStageEntryPoint(_currentIndex));
        //if (CameraManager.Instance != null)
        //    CameraManager.Instance.SnapCurrentCamera();
    }

    // 현재 스테이지 클리어 처리
    public void ClearCurrentStage() => _cleared[_currentIndex] = true;

    // 스테이지 진입 시 리셋 없이 해당 스테이지 세이브 포인트만 반환
    public Vector2 GetStageEntryPoint(int index)
    {
        if (index < 0 || index >= _instances.Count) return Vector2.zero;
        if (_instances[index].TryGetComponent(out Stage stage))
            return stage.GetRespawnPoint(_cleared[index]);
        return _instances[index].transform.position;
    }

    // 플레이어 사망 시 현재 스테이지 초기화 후 리스폰 위치 반환
    public Vector2 GetRespawnPoint()
    {
        ResetCurrentStage();

        if (_instances[_currentIndex].TryGetComponent(out Stage stage))
        {
            Vector2 point = stage.GetRespawnPoint(_cleared[_currentIndex]);
            Debug.Log($"[StageManager] 리스폰: stage={_currentIndex}, cleared={_cleared[_currentIndex]}, pos={point}, stagePos={stage.transform.position}");
            return point;
        }

        return _instances[_currentIndex].transform.position;
    }

    // 클리어된 스테이지: 재생성 없이 재활성화 (적 상태 유지)
    // 미클리어 스테이지: 프리팹에서 완전 재생성 (적 포함 전체 리셋)
    private void ResetCurrentStage()
    {
        int i = _currentIndex;

        if (!_cleared[i])
        {
            Destroy(_instances[i]);
            var fresh = Instantiate(stages[i], _stagePositions[i], Quaternion.identity);
            _instances[i] = fresh;

            if (fresh.TryGetComponent(out Stage newStage))
            {
                newStage.Init(i);
                //if (CameraManager.Instance != null)
                    //CameraManager.Instance.UpdateCameraAt(i, newStage.VirtualCamera);
            }
        }

        if (_instances[i].TryGetComponent(out Stage stage))
            stage.Activate();

        //if (CameraManager.Instance != null)
            //CameraManager.Instance.SwitchToCamera(i);
    }

    public Stage GetCurrentStage()
    {
        if (_instances[_currentIndex] != null &&
            _instances[_currentIndex].TryGetComponent(out Stage stage))
            return stage;
        return null;
    }

    // 스테이지 인덱스로 현재 스테이지 변경
    public void SetCurrentStage(int index)
    {
        if (index < 0 || index >= _instances.Count || index == _currentIndex) return;

        // 다음 스테이지 진입 시 현재 스테이지 클리어
        if (index > _currentIndex)
        {
            _cleared[_currentIndex] = true;
            OnStageClear?.Invoke(_currentIndex);
        }

        _currentIndex = index;
        UpdateActiveStages();
        //if (CameraManager.Instance != null)
        //    CameraManager.Instance.SwitchToCamera(index);
    }

    // 현재 ±1 범위만 활성화, 나머지 비활성화
    // 활성화 시 계산된 위치로 복원
    private void UpdateActiveStages()
    {
        for (int i = 0; i < _instances.Count; i++)
        {
            bool shouldBeActive = Mathf.Abs(i - _currentIndex) <= 1;

            if (shouldBeActive && !_instances[i].activeSelf)
            {
                _instances[i].transform.position = _stagePositions[i];
                _instances[i].SetActive(true);
            }
            else if (!shouldBeActive && _instances[i].activeSelf)
            {
                _instances[i].SetActive(false);
            }
        }
    }

    // 스테이지의 모든 Renderer를 합친 bounds 반환
    // Renderer가 없으면 BoxCollider2D로 대체
    private Bounds GetStageBounds(GameObject stage)
    {
        Renderer[] renderers = stage.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        if (stage.TryGetComponent(out BoxCollider2D col))
            return col.bounds;

        return new Bounds(stage.transform.position, Vector3.zero);
    }
}
