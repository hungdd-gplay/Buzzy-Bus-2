using System.Collections.Generic;
using BaseProject.Core.Feature;

/// <summary>
/// Item thu thập của các Challenge trong level (Weekly, Coin, Diamond, RoyalPass...).
/// Toàn bộ logic chung nằm ở CollectItemGameplayController.
/// </summary>
public class ChallengeGameplayController : CollectItemGameplayController<ChallengeType>
{
    protected override IEnumerable<ICollectItemSource> GetSources()
    {
        if (!GameController.IsFeatureOn(FeatureId.Challenge) || GameController.Instance.challengeController == null)
            yield break;
        foreach (var _challenge in GameController.Instance.challengeController.lstChallenges)
            if (_challenge != null)
                yield return _challenge;
    }

    protected override ChallengeType GetKey(ICollectItemSource source)
    {
        return ((ChallengeBase)source).typeChallenge;
    }

    protected override void SetUpFrame(ChallengeType key)
    {
        if (GamePlayController.Instance.itemShowFarmeController != null)
            GamePlayController.Instance.itemShowFarmeController.SetUpFrame(key);
    }

    protected override ItemShowFarme GetFrameByKey(ChallengeType key)
    {
        return GamePlayController.Instance.itemShowFarmeController != null
            ? GamePlayController.Instance.itemShowFarmeController.GetFarme(key)
            : null;
    }

    protected override void OnWinLevelHandle()
    {
        // Space Race tính theo số level thắng, không cần nhặt item
        var _raceChallenge = GameController.Instance.challengeController.GetChallenges(ChallengeType.RaceSpaceChallenge);
        if (_raceChallenge != null && _raceChallenge.IsChallengeActive() && RaceSpaceChallenge.IsRacing)
            _raceChallenge.AddItem(1);
    }
}
