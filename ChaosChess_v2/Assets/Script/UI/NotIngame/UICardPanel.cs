using System.Collections;
using UnityEngine;

public class UICardPanel : ButtonPanel
{
    public UICardAnim[] anims;
    public override void EnablePanel()
    {
        base.EnablePanel();
        StartCoroutine(spawnAnims());
    }

    public override void DisablePanel()
    {
        base.DisablePanel();
    }

    private IEnumerator spawnAnims()
    {
        for (int i = 0; i < anims.Length; i++)
        {
            // 카드 내용 추가
            // ...

            // 애니메이션 생성
            anims[i].gameObject.SetActive(true);
            anims[i].CardAnimation();

            yield return new WaitForSeconds(0.2f);
        }

        yield return new WaitForSeconds(0.5f);
        DisablePanel();
    }
}
