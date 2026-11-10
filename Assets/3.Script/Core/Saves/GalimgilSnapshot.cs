using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanGhost.Gameplay
{
    public class GalimgilSnapshot
    {
        // 기존 저장의 정수 값 규칙을 유지합니다. 0은 미지급, 1은 지급 완료입니다.
        public int Claimed { get; }
        public IReadOnlyList<int> InteractedObjects { get; }
        public GalimgilSnapshot(int claimed = 0, IEnumerable<int> interactedObjects = null)
        {
            if (claimed < 0 || claimed > 1) throw new ArgumentOutOfRangeException(nameof(claimed));
            int[] ids = (interactedObjects ?? Array.Empty<int>()).Distinct().OrderBy(id => id).ToArray();
            if (ids.Any(id => id <= 0)) throw new ArgumentException("상호작용 오브젝트 ID는 양수여야 합니다.", nameof(interactedObjects));
            Claimed = claimed; InteractedObjects = Array.AsReadOnly(ids);
        }
    }
}
