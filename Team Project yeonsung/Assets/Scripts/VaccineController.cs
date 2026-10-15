using UnityEngine;
using UnityEngine.UI;

public class VaccineController : MonoBehaviour
{
    [SerializeField] private Image vaccineImage;
    [SerializeField] private Sprite obtainedVaccineSprite;

    // This value survives scene changes during the current play session.
    private static bool isVaccine;

    public static bool IsVaccine => isVaccine;
    public static bool HasVaccine => IsVaccine;

    private void Start()
    {
        if (vaccineImage != null)
        {
            vaccineImage.enabled = IsVaccine;

            if (IsVaccine && obtainedVaccineSprite != null)
            {
                vaccineImage.sprite = obtainedVaccineSprite;
            }
        }
    }

    // A boss in another scene can call this static method to award a vaccine.
    public static void AcquireVaccine()
    {
        if (IsVaccine)
        {
            return;
        }

        isVaccine = true;
        Debug.Log("πÈΩ≈¿ª »πµÊ«ﬂΩ¿¥œ¥Ÿ.");
    }

    // This instance method also updates the icon when this scene is loaded.
    public void ObtainVaccine()
    {
        AcquireVaccine();

        if (vaccineImage != null)
        {
            if (obtainedVaccineSprite != null)
            {
                vaccineImage.sprite = obtainedVaccineSprite;
            }

            vaccineImage.enabled = true;
        }
    }

    // The infected gate calls this after the player presses F nearby.
    public static bool ConsumeVaccine()
    {
        if (!IsVaccine)
        {
            return false;
        }

        isVaccine = false;
        return true;
    }
}