using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class BossLungeHeadStandPlayModeTests
{
    [UnityTest]
    public IEnumerator MeasureBossHeadStandDuringLunge()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync("GamePlayScene_TestResult", LoadSceneMode.Single);
        yield return load;
        yield return null;
        yield return null;

        GameObject bossObject = GameObject.Find("Boss");
        Assert.IsNotNull(bossObject, "Boss object not found in GamePlayScene_TestResult.");

        GameObject playerObject = GameObject.Find("Player");
        Assert.IsNotNull(playerObject, "Player object not found in GamePlayScene_TestResult.");

        CharacterController bossController = bossObject.GetComponent<CharacterController>();
        CharacterController playerController = playerObject.GetComponent<CharacterController>();
        Assert.IsNotNull(bossController, "Boss CharacterController not found.");
        Assert.IsNotNull(playerController, "Player CharacterController not found.");

        Vector3 flatForward = bossObject.transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude <= 0.000001f)
        {
            flatForward = Vector3.forward;
        }

        flatForward.Normalize();
        Vector3 playerStart = bossObject.transform.position + flatForward * 15f;
        playerStart.y = 1f;
        playerObject.transform.position = playerStart;

        float elapsed = 0f;
        const float duration = 12f;
        const float positiveDeltaThreshold = 0.08f;
        const float planarNearThreshold = 1.2f;

        float maxBottomMinusPlayerTop = float.NegativeInfinity;
        float minPlanarAtPositiveDelta = float.PositiveInfinity;
        bool headStandDetected = false;

        while (elapsed < duration)
        {
            Vector3 bossPos = bossObject.transform.position;
            Vector3 playerPos = playerObject.transform.position;
            Vector3 planarDelta = playerPos - bossPos;
            planarDelta.y = 0f;
            float planarDistance = planarDelta.magnitude;

            float bossBottom = bossPos.y + bossController.center.y - (bossController.height * 0.5f);
            float playerTop = playerPos.y + playerController.center.y + (playerController.height * 0.5f);
            float delta = bossBottom - playerTop;

            if (delta > maxBottomMinusPlayerTop)
            {
                maxBottomMinusPlayerTop = delta;
            }

            if (delta > 0f && planarDistance < minPlanarAtPositiveDelta)
            {
                minPlanarAtPositiveDelta = planarDistance;
            }

            if (delta > positiveDeltaThreshold && planarDistance <= planarNearThreshold)
            {
                headStandDetected = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        string minPlanarText = float.IsPositiveInfinity(minPlanarAtPositiveDelta)
            ? "inf"
            : minPlanarAtPositiveDelta.ToString("F4");

        Debug.Log(
            $"[BossLungeHeadStandTest] " +
            $"detected={headStandDetected} " +
            $"maxBottomMinusPlayerTop={maxBottomMinusPlayerTop:F4} " +
            $"minPlanarAtPositiveDelta={minPlanarText}");

        Assert.Pass();
    }
}
