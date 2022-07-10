using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class AttackCenter : MonoBehaviour
{
    [SerializeField] private EffectData heartEffect;

    private HeadCenter headCenter;
    private CrystalHeart enemyCrystal;

    // targets
    private bool enemy, ally, crystalHeart;

    // states
    public bool StopAttack { get; set; }
    public bool Defeated { get; set; }


    public event UnityAction<GameObject> AddEnergyWhenFirstAttack;
    public event UnityAction<GameObject> RefreshStat;

    // Show Effects
    public event UnityAction<GameObject, List<EffectData>, int, bool> ShowDamageEffects;
    public event UnityAction<GameObject, int, bool> ShowDamage;
    public event UnityAction<GameObject, EffectData> ShowDamageEffect;
    public event UnityAction<GameObject> ShowMiss;

    // Animations
    public event UnityAction<GameObject, GameObject> InitTransforms;
    public event UnityAction<GameObject, GameObject, bool, bool> EnemyAnimation;
    public event UnityAction<GameObject, GameObject, bool, bool> MultiEnemyAnimation;
    public event UnityAction<GameObject, GameObject, bool> CrystalAnimation;
    public event UnityAction<GameObject, GameObject, bool> AllyAnimation;
    public event UnityAction<GameObject> EndAnimation;

    private void Awake()
    {
        InitHelper();
    }

    private void OnEnable()
    {
        headCenter.StartAttack += InitAttack;
    }
    private void OnDisable()
    {
        headCenter.StartAttack -= InitAttack;
    }


    // Attack
    private void InitAttack(GameObject token, GameObject target)
    {
        StartAttackHelper(token, target);
        var firstAttack = true;

        if (enemy) { EnemyAttackHelper(token, target, firstAttack); }
        else if (crystalHeart) { CrystalAttackHelper(token, target, firstAttack); }
        else if (ally) { AllyAttackHelper(token, target, firstAttack); }
    }


    // First Attack
    private void EnemyAttackHelper(GameObject token, GameObject target, bool firstAttack)
    {
        var data = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        var tokenOverride = token.GetComponent<TokenOverride>();
        tokenOverride.PreAttackInit(target);  

        if (data.WarroirType) { SingleAttack(token, target, firstAttack); }
        else
        {
            if (!tokenHelper.Multishot) { SingleAttack(token, target, firstAttack); }
            else { InitMultiAttack(token, target, firstAttack); }
        }
    }
    private void CrystalAttackHelper(GameObject token, GameObject crystal, bool firstAttack)
    {
        var crit = CriticalAttack(token);
        CrystalAnimation?.Invoke(token, crystal, firstAttack);
        if (firstAttack) { StartCoroutine(FirstCrystalAttack(token, crystal, crit)); }
        else { StartCoroutine(NewAttack(token, crystal, crit)); }
    }
    private void AllyAttackHelper(GameObject token, GameObject allyToken, bool firstAttack)
    {
        var crit = false;
        AllyAnimation?.Invoke(token, allyToken, firstAttack);
        if (firstAttack) { StartCoroutine(FirstTokenAttack(token, allyToken, crit)); }
        else { StartCoroutine(NewAttack(token, allyToken, crit)); }
    }


    // Single Attack
    private void SingleAttack(GameObject token, GameObject target, bool firstAttack)
    {
        var crit = CriticalAttack(token);
        EnemyAnimation?.Invoke(token, target, crit, firstAttack);

        if (firstAttack) { StartCoroutine(FirstTokenAttack(token, target, crit)); }
        else { StartCoroutine(NewAttack(token, target, crit)); }
    }


    // Multi Attack
    private void InitMultiAttack(GameObject token, GameObject mainTarget, bool fisrtAttack)
    {
        var data = token.GetComponent<Token>();
        var guardian = false;
        var tokenHelper = token.GetComponent<TokenHelper>();

        var mainTargetHelper = mainTarget.GetComponent<TokenHelper>();
        var enemyTargets = data.MyPlayer.BattleData.EnemyField.TargetsOnField;
        var enemyGuardians = data.MyPlayer.BattleData.EnemyField.GuardiansOnField;

        var multishotCount = tokenHelper.MultishotValue;

        mainTargetHelper.CanBeAttacked = false;
        enemyTargets.Remove(mainTarget);

        if (mainTargetHelper.Guardian) { guardian = true; enemyGuardians.Remove(mainTarget); }
        SingleAttack(token, mainTarget, fisrtAttack);

        List<GameObject> attackedByMultishot = new();
        if (enemyTargets.Count > 0)
        {
            if (guardian)
            {
                for (int i = 0; i < multishotCount; i++)
                {
                    if (enemyGuardians.Count > 0)
                    {
                        var random = Random.Range(0, enemyGuardians.Count);
                        var target = enemyGuardians[random];

                        MultiAttackHelper(token, target, fisrtAttack);
                        enemyGuardians.Remove(target);
                        attackedByMultishot.Add(target);
                    }

                }
            }
            else
            {
                for (int i = 0; i < multishotCount; i++)
                {
                    if (enemyTargets.Count > 0)
                    {
                        var random = Random.Range(0, enemyTargets.Count);
                        var target = enemyTargets[random];

                        MultiAttackHelper(token, target, fisrtAttack);
                        enemyTargets.Remove(target);
                        attackedByMultishot.Add(target);
                    }

                }
            }

        }
        if (!mainTargetHelper.IsDead)
        {
            if (!guardian) { enemyTargets.Add(mainTarget); }
            else { enemyGuardians.Add(mainTarget); }
        }
        if (attackedByMultishot.Count > 0)
        {
            for (int i = 0; i < attackedByMultishot.Count; i++)
            {
                var attacked = attackedByMultishot[i];
                enemyTargets.Add(attacked);
            }
        }
    }

    private void MultiAttackHelper(GameObject token, GameObject target, bool firstAttack)
    {
        var crit = CriticalAttack(token);
        MultiEnemyAnimation?.Invoke(token, target, crit, firstAttack);
        StartCoroutine(MultiAttack(token, target, crit, firstAttack));
    }

    private IEnumerator MultiAttack(GameObject token, GameObject target, bool crit, bool firstAttack)
    {
        var tokenData = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        var tokenOverride = token.GetComponent<TokenOverride>();

        var targetData = target.GetComponent<Token>();
        var targetHelper = target.GetComponent<TokenHelper>();
        var targetOverride = target.GetComponent<TokenOverride>();

        targetHelper.Animated = true;

        var evasion = Evasion(target);

        if (firstAttack) { yield return new WaitForSeconds(1.05f); }
        else { yield return new WaitForSeconds(0.65f); }

        if (!evasion) { var sinlge = false; ApplyDamage(token, target, crit, sinlge); }
        else { targetOverride.WhenEvaded(); ShowMiss?.Invoke(target); }



        if (!targetHelper.IsDead)
        {
            targetHelper.Animated = false;
            if (!evasion) { targetOverride.WhenFirstAttacked(token); AddEnergyWhenFirstAttack?.Invoke(target); }
        } 
        else if (!tokenHelper.IsDead)
        {
            if (targetHelper.IsDead || targetData.CurrentHealth == 0) { tokenOverride.WhenKillToken(); }
        }
    }


    // First Attack
    private IEnumerator FirstTokenAttack(GameObject token, GameObject target, bool crit)
    {
        var tokenData = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        var tokenOverride = token.GetComponent<TokenOverride>();

        var targetData = target.GetComponent<Token>();
        var targetHelper = target.GetComponent<TokenHelper>();
        var targetOverride = target.GetComponent<TokenOverride>();


        tokenHelper.CanAttackLight.enabled = false;
        targetHelper.WhenTargetLight.enabled = true;

        targetHelper.Animated = true;
        tokenHelper.Animated = true;

        var evasion = Evasion(target);

        if (enemy)
        {
            if (tokenData.WarroirType) { yield return new WaitForSeconds(0.75f); }
            else if (tokenData.RangedType) { yield return new WaitForSeconds(1.05f); }
            else if (tokenData.AssassinType) { yield return new WaitForSeconds(1.15f); }

            if (!evasion)
            {
                var sinlge = true;
                ApplyDamage(token, target, crit, sinlge);

                if (!targetHelper.IsDead)
                { targetOverride.WhenFirstAttacked(token); AddEnergyWhenFirstAttack?.Invoke(target); }
                else
                {
                    targetOverride.WhenEvaded();
                    ShowMiss?.Invoke(target);
                }

                CounterAttackCheck(target, token, crit);
                StopAttackCheck(token, target);
            }
            else
            {
                if (tokenData.WarroirType) { yield return new WaitForSeconds(0.3f); }
                else if (tokenData.RangedType) { yield return new WaitForSeconds(0.6f); }
                else if (tokenData.AssassinType) { yield return new WaitForSeconds(0.7f); }

                ApplyAllyInteraction(token, target);
            }

            if (!evasion)
            {
                // Если атака по цели прошла!
                if (!tokenHelper.IsDead)
                {
                    tokenOverride.FirstAttack(target);

                    if (!tokenHelper.AbilityAttacked) { AddEnergyWhenFirstAttack?.Invoke(token); }
                }
            }



            var currentAttacks = (tokenData.AttacksCount - 1) + tokenHelper.AttacksCountAdds;
            tokenHelper.AttacksCountAdds = 0;

            // Есть ли еще атаки?
            if (currentAttacks > 0 && !StopAttack)
            {
                StartCoroutine(InitNewAttack(token, target, currentAttacks));
            }
            else
            {
                if (!tokenData.RangedType) { yield return new WaitForSeconds(0.4f); }
                EndAttack(token, target);
            }

        }
    }

    private IEnumerator FirstCrystalAttack(GameObject token, GameObject target, bool crit)
    {
        var tokenData = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        var tokenOverride = token.GetComponent<TokenOverride>();


        tokenHelper.CanAttackLight.enabled = false;

        enemyCrystal.CanBeAttackedLight.gameObject.SetActive(false);
        enemyCrystal.WhenTargetForAttack.gameObject.SetActive(true);

        if (tokenData.WarroirType) { yield return new WaitForSeconds(0.75f); }
        if (tokenData.RangedType) { yield return new WaitForSeconds(1.05f); }
        if (tokenData.AssassinType) { yield return new WaitForSeconds(1.15f); }

        enemyCrystal.ApplyDamage(tokenData.CurrentAttack, headCenter.RedPlayer, crit);

        if (!tokenHelper.IsDead) { tokenOverride.FirstAttack(target); if (!tokenHelper.AbilityAttacked) { AddEnergyWhenFirstAttack?.Invoke(token); } }

        var currentAttacks = (tokenData.AttacksCount - 1) + tokenHelper.AttacksCountAdds;
        tokenHelper.AttacksCountAdds = 0;

        // Есть ли еще атаки?
        if (currentAttacks > 0 && !StopAttack)
        {
            tokenHelper.Animated = true;
            StartCoroutine(InitNewAttack(token, target, currentAttacks));
        }
        else
        {
            if (!tokenData.RangedType) { yield return new WaitForSeconds(0.4f); }
            EndAttack(token, target);
        }

    }

    // Next Attack
    private IEnumerator InitNewAttack(GameObject token, GameObject target, int attacksCount)
    {
        var data = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        var firstAttack = false;

        if (!data.RangedType) { yield return new WaitForSeconds(0.5f); }

        if (attacksCount > 0 && !StopAttack)
        {
            attacksCount--;

            if (enemy) { EnemyAttackHelper(token, target, firstAttack); }
            else if (crystalHeart) { CrystalAttackHelper(token, target, firstAttack); }
            else if (ally) { AllyAttackHelper(token, target, firstAttack); }

            if (data.RangedType) { yield return new WaitForSeconds(0.7f); }
            else { yield return new WaitForSeconds(0.35f); }

            attacksCount += tokenHelper.AttacksCountAdds;
            tokenHelper.AttacksCountAdds = 0;

            StartCoroutine(InitNewAttack(token, target, attacksCount));
        }
        else { EndAttack(token, target); }
    }

    private IEnumerator NewAttack(GameObject token, GameObject target, bool crit)
    {
        var data = token.GetComponent<Token>();
   
        if (data.RangedType) { yield return new WaitForSeconds(0.65f); }
        else if (data.AssassinType) { yield return new WaitForSeconds(0.4f); }
        else { yield return new WaitForSeconds(0.30f); }

        if (enemy)
        {
            var evasion = Evasion(target);

            if (!evasion)
            {
                var sinlge = true;
                ApplyDamage(token, target, crit, sinlge);
                StopAttackCheck(token, target);
            }
            else
            {
                var tokenOverride = target.GetComponent<TokenOverride>();
                tokenOverride.WhenEvaded();
                ShowMiss?.Invoke(target);
            }

        }
        else if (crystalHeart) 
        { 
            var attack = data.CurrentAttack; 
            enemyCrystal.ApplyDamage(attack, headCenter.RedPlayer, crit); 
        }
    }

    private void StopAttackCheck(GameObject token, GameObject target)
    {
        var data = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        var tokenOverride = token.GetComponent<TokenOverride>();

        var targetData = target.GetComponent<Token>();
        var targetHelper = target.GetComponent<TokenHelper>();
        var targetOverride = target.GetComponent<TokenOverride>();

        if (tokenHelper.IsDead || data.CurrentHealth == 0 ||
            targetHelper.IsDead || targetData.CurrentHealth == 0)
        {
            StopAttack = true;

            if (!tokenHelper.IsDead && targetHelper.IsDead) { tokenOverride.WhenKillToken(); }
            else if (tokenHelper.IsDead && !targetHelper.IsDead) { targetOverride.WhenKillToken(); }
        }
    }



    private void CounterAttackCheck(GameObject whoAttacks, GameObject target, bool crit)
    {
        var tokenData = whoAttacks.GetComponent<Token>();
        var tokenHelper = whoAttacks.GetComponent<TokenHelper>();

        var targetData = target.GetComponent<Token>();

        if (tokenData.AttackPossible || tokenData.CurrentAttack > 0)
        {
            if (!tokenHelper.Frostbite)
            {
                if (tokenHelper.CounterAttackMelee && targetData.WarroirType ||
                    tokenHelper.CounterAttackRange && targetData.RangedType)
                {
                    CounterAttack(whoAttacks, target, crit);
                }
            }
        }
    }

    private void ApplyDamage(GameObject token, GameObject target, bool crit, bool single)
    {
        var currentDamage = SetGetDamage(token, target, crit, single);

        List<EffectData> damageEffects = new();

        if (single) { ApplyFeatureEffects(token, target, currentDamage, damageEffects); }

        PlayDamageSound(target, crit);

        RefreshStat?.Invoke(target);
        RefreshStat?.Invoke(token);

        ShowDamageEffects?.Invoke(target, damageEffects, currentDamage, crit);
    }

    private int SetGetDamage(GameObject token, GameObject target, bool crit, bool single)
    {
        var tokenData = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        var tokenOverride = token.GetComponent<TokenOverride>();

        var targetData = target.GetComponent<Token>();
        var targetHelper = target.GetComponent<TokenHelper>();
        var targetOverride = target.GetComponent<TokenOverride>();

        var ctitATK = tokenData.CriticalDMG;
        int dmgReduce = 1;
        if (tokenHelper.DMGReduce > 0) { dmgReduce = 100 / tokenHelper.DMGReduce; }

        int currentDamage;

        if (single) 
        {
            if (!tokenHelper.Desolator)
            {
                currentDamage = ((tokenData.CurrentAttack + tokenHelper.DMGAdds) - targetData.CurrentDefence) - targetHelper.DMGReduce;
            }
            else { currentDamage = (tokenData.CurrentAttack + tokenHelper.DMGAdds) - targetHelper.DMGReduce; }

        }
        else { currentDamage = (((tokenData.CurrentAttack + tokenHelper.ATKAdds) - targetData.CurrentDefence) / dmgReduce) - targetHelper.DMGReduce; }

        tokenHelper.DMGAdds = 0;

        if (currentDamage > 0)
        {
            if (targetHelper.Protected) { ApplyProtector(targetHelper.Protector, targetHelper); }

            if (crit)
            {
                tokenOverride.CriticalAttack(target);
                // AUTO Округли в большую степень
                var critdamage = currentDamage * ctitATK;
                if (critdamage > 1 && critdamage < 2) { critdamage = 2; }
                else if (critdamage > 2 && critdamage < 3) { critdamage = 3; }

                currentDamage = (int)critdamage;
            }

            tokenOverride.WhenAttacks(target);
            targetOverride.WhenAttacked();

            targetData.CurrentHealth -= currentDamage;
        }
        else { currentDamage = 0; }


  



        return currentDamage;
    }


    // Sounds
    private void PlayDamageSound(GameObject target, bool crit)
    {
        if (!crit) { target.GetComponent<AudioSource>().PlayOneShot(arrowHit); }
        else { target.GetComponent<AudioSource>().PlayOneShot(critHit); }

    }


    private void ApplyAllyInteraction(GameObject token, GameObject target)
    {
        var tokenData = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        var tokenOverride = token.GetComponent<TokenOverride>();

        var targetData = target.GetComponent<Token>();
        var targetHelper = target.GetComponent<TokenHelper>();


        tokenOverride.AllyAttack(target);
        RefreshStat?.Invoke(target);
    }

    private void ApplyProtector(GameObject protector, TokenHelper targetHelper)
    {
        var protectorCrit = false;
        var protectorData = protector.GetComponent<Token>();

        // efffect protector! reducer
        var currentProtectorDamage = targetHelper.DMGReduce - protectorData.CurrentDefence;

        protectorData.CurrentHealth -= currentProtectorDamage;

        ShowDamage?.Invoke(protector, currentProtectorDamage, protectorCrit);
        RefreshStat?.Invoke(protector);
    }




    private void CounterAttack(GameObject token, GameObject target, bool crit)
    {

        var tokenData = token.GetComponent<Token>(); var tokenHelper = token.GetComponent<TokenHelper>();
        var tokenOverride = token.GetComponent<TokenOverride>();

        var targetData = target.GetComponent<Token>(); var targetHelper = target.GetComponent<TokenHelper>();
        var targetOverride = target.GetComponent<TokenOverride>();

        var ctitATK = tokenData.CriticalDMG;
        var currentDamage = (tokenData.CurrentAttack + tokenHelper.CounterAttackATKadds) - targetData.CurrentDefence;
        var critdamage = 0.0f;

        if (currentDamage > 0)
        {
            if (tokenHelper.Desolator) { currentDamage = tokenData.CurrentAttack + tokenHelper.ATKAdds; }
            if (crit)
            {
                critdamage = currentDamage * ctitATK;
                currentDamage = (int)critdamage;
            }
        }
        else { currentDamage = 0; }

        List<EffectData> damageEffects = new();

        targetData.CurrentHealth -= currentDamage;
        //ApplyFeatureEffects(token, target, currentDamage, damageEffects);

        RefreshStat?.Invoke(target);
        RefreshStat?.Invoke(token);

        ShowDamageEffects?.Invoke(target, damageEffects, currentDamage, crit);
    }

    private void EndAttack(GameObject token, GameObject target)
    {
        var tokenHelper = token.GetComponent<TokenHelper>();

        if (!tokenHelper.IsDead) { EndAnimation?.Invoke(token); }

        if (tokenHelper.AbilityAttacked)
        { 
            var tokenOverride = token.GetComponent<TokenOverride>(); 
            tokenOverride.AbilityAttackEnd();
        }

        if (enemy || ally)
        {
            var targetHelper = target.GetComponent<TokenHelper>();

            if (!targetHelper.IsDead)
            { targetHelper.WhenTargetLight.enabled = false; targetHelper.Animated = false; }
        }
        else
        {
            enemyCrystal.WhenTargetForAttack.gameObject.SetActive(false);
            enemyCrystal.CrystalCanBeAttacked = false;
        }
    }



    private void ApplyFeatureEffects(GameObject token, GameObject target, int damage, List<EffectData> effects)
    {
        var tokenHelper = token.GetComponent<TokenHelper>();
        var targetHelper = target.GetComponent<TokenHelper>();

        if (damage > 0)
        {
            if (tokenHelper.Vampirism)
            {
                ApplyVampirism(token, target);
                effects.Add(tokenHelper.VampirismEffect);
            }
        }

        if (targetHelper.Spike)
        {
            ApplySpike(token, target);
            ShowDamageEffect?.Invoke(token, targetHelper.SpikeEffect);
        }
    }

    private void ApplyVampirism(GameObject token, GameObject target)
    {
        var tokenData = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        var tokenOverride = token.GetComponent<TokenOverride>();

        var targetData = target.GetComponent<Token>();

        if (tokenHelper.VampirismEffect != null)
        {
            tokenOverride.VampirismCasts();
            var vampirismEffect = tokenHelper.VampirismEffect;
            var vampirism = vampirismEffect.CurrentValue;

            targetData.CurrentHealth -= vampirism;
            tokenData.CurrentHealth += vampirism;
        }

    }

    private void ApplySpike(GameObject whoAttacks, GameObject whoHasSpike)
    {
        var tokenData = whoAttacks.GetComponent<Token>();
        var targetHelper = whoHasSpike.GetComponent<TokenHelper>();

        var spikeDamage = targetHelper.SpikeEffect.CurrentValue;
        tokenData.CurrentHealth -= spikeDamage;
        RefreshStat?.Invoke(whoAttacks);
    }

    private bool CriticalAttack(GameObject token)
    {
        var data = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();

        var critChance = data.CriticalChance + tokenHelper.CriticalChanceAdds;
        var critNumber = Random.Range(0, 100);
        var crit = false;

        if (critNumber <= critChance) { crit = true; }
        return crit;
    }
    private bool Evasion(GameObject token)
    {
        var data = token.GetComponent<Token>();
        var evasionChance = data.Evasion;
        var evasionNumber = Random.Range(0, 100);
        var evasion = false;

        if (data.Evasion > 0) { if (evasionNumber <= evasionChance) { evasion = true; } }
        return evasion;
    }



    private void StartAttackHelper(GameObject token, GameObject target)
    {
        var tokenHelper = token.GetComponent<TokenHelper>();
        tokenHelper.CanAttack = false;
        tokenHelper.Attacked = true;

        SetTarget(target);
        InitTransforms?.Invoke(token, target);
    }

    private void SetTarget(GameObject target)
    {
        StopAttack = false; enemy = false; crystalHeart = false; ally = false;

        if (target.GetComponent<RedPlayer>()) { enemy = true; }
        else if (target.GetComponent<CrystalHeart>()) { crystalHeart = true; }
        else if (target.GetComponent<BluePlayer>()) { ally = true; }
    }


    private void InitHelper()
    {
        headCenter = FindObjectOfType<HeadCenter>();
        enemyCrystal = headCenter.RedPlayer.BattleData.Crystal;
    }
}