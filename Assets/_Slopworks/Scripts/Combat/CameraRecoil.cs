using UnityEngine;

// Place on the FPSCamera GameObject.
// Call ApplyRecoil() from WeaponBehaviour.OnFire after a successful shot.
// Runs in LateUpdate so it layers on top of PlayerController.Look() each frame.
public class CameraRecoil : MonoBehaviour
{
    [SerializeField] private float _kickStrength = 2.5f;
    [SerializeField] private float _horizontalSpread = 0.5f;
    [SerializeField] private float _recoverySpeed = 10f;

    // current recoil offset, separate from the base rotation set by PlayerController
    private Quaternion _recoilOffset = Quaternion.identity;

    public void ApplyRecoil()
    {
        float vertical = _kickStrength;
        float horizontal = Random.Range(-_horizontalSpread, _horizontalSpread);

        // accumulate onto existing offset so rapid fire stacks naturally
        _recoilOffset = Quaternion.Euler(-vertical, horizontal, 0f) * _recoilOffset;
    }

    private void LateUpdate()
    {
        // decay the recoil offset toward identity
        _recoilOffset = Quaternion.Lerp(_recoilOffset, Quaternion.identity, _recoverySpeed * Time.deltaTime);

        // PlayerController already wrote localRotation this frame; multiply our offset on top
        transform.localRotation = transform.localRotation * _recoilOffset;
    }
}
