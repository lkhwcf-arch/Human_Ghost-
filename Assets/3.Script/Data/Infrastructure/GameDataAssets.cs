using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Data.Infrastructure
{
    [CreateAssetMenu(menuName = "인귀/데이터/데이터 에셋 목록")]
    public sealed class GameDataAssets : ScriptableObject
    {
        [SerializeField] private List<TextAsset> _csvFiles = new List<TextAsset>();
        [SerializeField] private List<TextAsset> _jsonFiles = new List<TextAsset>();

        public TextAssetCollectionSource CreateSource()
            => new TextAssetCollectionSource(_csvFiles, _jsonFiles);
    }
}
