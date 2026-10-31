using System;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Storage;
using UnityEngine;

namespace HumanGhost.Data.Infrastructure
{
    // 씬이 초기화 객체와 취소 수명을 소유합니다. 정적 전역 저장소를 만들지 않습니다.
    public sealed class GameDataInitializer : MonoBehaviour
    {
        [SerializeField] private GameDataAssets _assets = null;
        private CancellationTokenSource _lifetime;
        private DataBootstrapper _bootstrapper;

        public event Action<IReadOnlyDataCatalog> Initialized;
        public IReadOnlyDataCatalog Data => _bootstrapper?.Data;

        private void Awake()
        {
            _lifetime = new CancellationTokenSource();
        }

        private async void Start()
        {
            try { await InitializeAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Debug.LogError($"데이터 초기화에 실패했습니다.\n{error.Message}", this);
            }
        }

        public Task<IReadOnlyDataCatalog> InitializeAsync()
        {
            if (this == null || _lifetime == null)
                throw new InvalidOperationException("씬의 초기화 객체가 준비되지 않았거나 종료되었습니다.");
            if (_bootstrapper == null)
            {
                if (_assets == null) throw new InvalidOperationException("데이터 에셋 목록을 연결해 주세요.");
                _bootstrapper = new DataBootstrapper(_assets.CreateSource(), path => path, ReportConsumerError);
                _bootstrapper.Initialized += OnInitialized;
            }
            return _bootstrapper.InitializeAsync(_lifetime.Token);
        }

        private void OnInitialized(IReadOnlyDataCatalog catalog)
        {
            Debug.Log("데이터 초기화가 완료되었습니다.", this);
            DataReadyNotification.Publish(Initialized, catalog, ReportConsumerError);
        }

        private void ReportConsumerError(string consumer, Exception error)
            => Debug.LogError($"데이터 준비는 완료됐지만 소비자 처리에 실패했습니다. 소비자: {consumer}\n{error}", this);

        private void OnDestroy()
        {
            if (_bootstrapper != null) _bootstrapper.Initialized -= OnInitialized;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            Initialized = null;
        }
    }
}
