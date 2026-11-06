using UnityEngine;

namespace HeosuguiTrial
{
    public sealed class HeosuguiDemo : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer reference;
        [SerializeField] private Transform arm;
        [SerializeField] private Transform forearm;
        [SerializeField] private Sprite[] frames;
        private Quaternion restArm, restForearm;
        private float elapsed;

        private void Awake()
        {
            restArm = arm.localRotation;
            restForearm = forearm.localRotation;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float phase = Mathf.Repeat(elapsed * 8f / frames.Length, 1f);
            float swing = Mathf.SmoothStep(0, 1, Mathf.PingPong(phase * 2f, 1f));
            arm.localRotation = restArm * Quaternion.AngleAxis(-17f * swing, Vector3.forward);
            forearm.localRotation = restForearm * Quaternion.AngleAxis(-9f * swing, Vector3.forward);
            reference.sprite = frames[Mathf.Min((int)(phase * frames.Length), frames.Length - 1)];
        }
    }
}
