using System.Collections;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Events;
using System.Collections.Generic;

public class AttackCenter : AttackAnimationCenter
{
    public AiAttackPhase AiAttack { get; set; }

    [SerializeField] private EffectData heartEffect;
    [SerializeField] private AudioClip arrowHit;
    [SerializeField] private AudioClip critHit;

    
    private HeadCenter headCenter;
    private Players players;
    private TurnSystemCenter turnSystem;
    private CrystalHeart enemyCrystal;


    // targets
    private bool enemy;
    private bool ally;
    private bool crystalHeart;


    // transform
    private Vector3 getReadyPos;
    private Vector3[] waypoints;

    private Vector3 tokenPos;
    private Vector3 tokenRot;

    private Vector3 targetPos;
    private Vector3 targetRot;

    // states
    public bool StopAttack { get; set; }
    public bool Defeated { get; set; }



    public event UnityAction<GameObject> RefreshStat;

    public event UnityAction<GameObject, List<EffectData>, int, bool> ShowDamageEffects;
    public event UnityAction<GameObject, EffectData> ShowDamageEffect;
    public event UnityAction<GameObject, int, bool> ShowDamage;
    public event UnityAction<GameObject, EffectData, bool> ShowEffect;
    public event UnityAction<GameObject> ShowMiss;


    public event UnityAction<GameObject> AddEnergyInAttack;

    private void Awake()
    {
        InitHelper();
    }

    private void OnEnable()
    {
        headCenter.StartAttack += InitAttack;
        turnSystem.CanAttackCheck += CanAttackCheck;
    }
    private void OnDisable()
    {
        headCenter.StartAttack += InitAttack;
        turnSystem.CanAttackCheck -= CanAttackCheck;
    }


    // Checks
    public void SummonSicknessCheck(Player player)
    {
        var field = player.BattleData.MyField;

        for (int i = 0; i < field.TargetsOnField.Count; i++)
        {
            var token = field.TargetsOnField[i];
            var tokenHelper = token.GetComponent<TokenHelper>();

            if (!player.BattleData.MyTurn && tokenHelper.Summoned)
            {
                tokenHelper.SummonSickness = false;
                tokenHelper.SummoningSicknessLight.enabled = false;
            }
        }
    }
    public void CanAttackCheck()
    {
        for (int i = 0; i < players.PlayersInBattle.Count; i++)
        {
            var player = players.PlayersInBattle[i];
            var field = player.BattleData.MyField;

            if (player.AI)
            {
                if (AiAttack == null)
                {
                    AiAttack = FindObjectOfType<AiAttackPhase>();
                }
                if (AiAttack.CanAttackList.Count > 0)
                {
                    AiAttack.CanAttackList.Clear();
                }
            }

            for (int x = 0; x < field.TargetsOnField.Count; x++)
            {
                var token = field.TargetsOnField[x];
                var data = token.GetComponent<Token>();
                var tokenHelper = token.GetComponent<TokenHelper>();

                tokenHelper.Attacked = false;

                if (data.AttackPossible || data.CurrentAttack > 0)
                {
                    if (player.BattleData.MyTurn && tokenHelper.Summoned &&
                        !tokenHelper.SummonSickness && !tokenHelper.Stun
                        && !tokenHelper.Frostbite && !tokenHelper.Petrify)
                    {
                        tokenHelper.CanAttack = true;


                        if (data.WarroirType && tokenHelper.Rooted)
                        {
                            tokenHelper.CanAttack = false;
                        }


                        if (tokenHelper.CanAttack)
                        {
                            tokenHelper.CanAttackLight.enabled = true;
                            if (player.AI) { AiAttack.CanAttackList.Add(token); }
                        }
                    }
                    else
                    {
                        tokenHelper.CanAttack = false;
                        tokenHelper.CanAttackLight.enabled = false;
                    }
                }
            }
        }
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
        CrystalAnimation(token, crystal, firstAttack);
        if (firstAttack)  { StartCoroutine(FirstCrystalAttack(token, crystal)); }
    }
    private void AllyAttackHelper(GameObject token, GameObject allyToken, bool firstAttack)
    {
        var crit = false;
        AllyAnimation(token, allyToken, firstAttack);
        if (firstAttack) { StartCoroutine(FirstTokenAttack(token, allyToken, crit)); }
        else { StartCoroutine(NewAttack(token, allyToken, crit)); }
    }


    // Single Attack
    private void SingleAttack(GameObject token, GameObject target, bool firstAttack)
    {
        var crit = CriticalAttack(token);
        EnemyAnimation(token, target, crit, firstAttack);

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
        MultiEnemyAnimation(token, target, crit, firstAttack);
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

        //tokenHelper.CanAttackLight.enabled = false;
        //targetHelper.WhenTargetLight.enabled = true;

        targetHelper.Animated = true;

        var evasion = Evasion(target);

        if (firstAttack) { yield return new WaitForSeconds(1.05f); }
        else { yield return new WaitForSeconds(0.65f); }

        if (!evasion) { ApplyDamageByMulti(token, target, crit); }
        else { targetOverride.WhenEvaded(); ShowMiss?.Invoke(target); }

        CheckForDeath(token, target);


        if (!targetHelper.IsDead)
        {
            targetHelper.Animated = false;
            if (!evasion) { targetOverride.WhenFirstAttacked(token); AddEnergyInAttack?.Invoke(target); }
        }
    }


    // Animations
    private void EnemyAnimation(GameObject token, GameObject target, bool crit, bool firstAttack)
    {
        var data = token.GetComponent<Token>();

        Sequence tokenSequence = DOTween.Sequence();
        Sequence targetSequence = DOTween.Sequence();

        // ������ ����� �� �����
        var targetGetDamagePos = new Vector3(targetPos.x + 0.75f, targetPos.y, targetPos.z);
        var targetGetDamageRot = new Vector3(targetRot.x, targetRot.y, targetRot.z - 1.5f);

        if (crit)
        {
            targetGetDamagePos = new Vector3(targetPos.x + 2f, targetPos.y, targetPos.z);
            targetGetDamageRot = new Vector3(targetRot.x, targetRot.y, targetRot.z - 2.5f);
        }

        if (data.WarroirType)
        {
            var targetTransformForToken = new Vector3(targetPos.x - 4f, targetPos.y, targetPos.z);
            var AfterHitPositionByWarroir = new Vector3(targetPos.x - 7f, targetPos.y, targetPos.z);
            var HitEffect = new Vector3(tokenRot.x + 3.5f, tokenRot.y - 1, tokenRot.z - 2.5f);

            if (firstAttack)
            {
                tokenSequence.Append(token.transform.DOMove(getReadyPos, 0.55f));
                tokenSequence.Insert(0.55f, token.transform.DOJump(targetTransformForToken, 3, 1, 0.25f));
                tokenSequence.Insert(0.8f, token.transform.DOMove(AfterHitPositionByWarroir, 0.45f).OnStepComplete(Kill));
                tokenSequence.Insert(0.8f, token.transform.DORotate(HitEffect, 0.15f).SetLoops(2, LoopType.Yoyo));

                // ���� �������� �����
                targetSequence.Insert(0.8f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo));
                targetSequence.Insert(0.8f, target.transform.DORotate(targetGetDamageRot, 0.15f).SetLoops(2, LoopType.Yoyo));
            }
            else
            {
                tokenSequence.Append(token.transform.DOJump(targetTransformForToken, 1, 1, 0.3f));
                tokenSequence.Insert(0.3f, token.transform.DOMove(AfterHitPositionByWarroir, 0.55f)).OnStepComplete(Kill);
                tokenSequence.Insert(0.3f, token.transform.DORotate(HitEffect, 0.15f).SetLoops(2, LoopType.Yoyo));

                // ���� �������� �����
                targetSequence.Insert(0.3f, target.transform.DORotate(targetGetDamageRot, 0.15f).SetLoops(2, LoopType.Yoyo));
                targetSequence.Insert(0.3f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo));
            }

        }
        else if (data.RangedType)
        {
            var projectile = Instantiate(data.Projectile, token.transform);
            waypoints[0] = target.transform.position;

            var AfterArrowPosition = new Vector3(tokenPos.x - 1.25f, tokenPos.y + 3.5f, tokenPos.z);
            var AfterArrowPositionReturn = new Vector3(tokenPos.x + 0.5f, tokenPos.y + 3.5f, tokenPos.z);

            if (firstAttack)
            {
                tokenSequence.Append(token.transform.DOMove(getReadyPos, 0.45f));
                // ����������� 
                tokenSequence.Insert(0.45f, token.transform.DOMove(AfterArrowPosition, 0.35f));
                //����� �������, �������� ���������.
                tokenSequence.Insert(0.8f, projectile.transform.DOPath(waypoints, 0.25f, PathType.CatmullRom).SetLookAt(0.01f)).OnComplete(KillProjectile);
                //������ �� ������� �������
                tokenSequence.Insert(0.8f, token.transform.DOMove(AfterArrowPositionReturn, 0.20f));

                //���� �������� ����
                targetSequence.Insert(1.05f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo)).OnComplete(Kill);
                targetSequence.Insert(1.05f, target.transform.DORotate(targetGetDamageRot, 0.15f).SetLoops(2, LoopType.Yoyo));
            }
            else
            {
                tokenSequence.Append(token.transform.DOMove(AfterArrowPosition, 0.35f));

                //����� �������, �������� ���������.
                tokenSequence.Insert(0.35f, projectile.transform.DOPath(waypoints, 0.25f, PathType.CatmullRom).SetLookAt(0.01f)).OnStepComplete(KillProjectile);
                //������ �� ������� �������
                tokenSequence.Insert(0.35f, token.transform.DOMove(AfterArrowPositionReturn, 0.20f));
                //���� �������� ����
                targetSequence.Insert(0.6f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo));
                targetSequence.Insert(0.6f, target.transform.DORotate(targetGetDamageRot, 0.15f).SetLoops(2, LoopType.Yoyo)).OnStepComplete(Kill);
            }

            void KillProjectile()
            {
                if (projectile != null) { Destroy(projectile); }
            }
        }
        else if (data.AssassinType)
        {
            var targetsBack = new Vector3(targetPos.x + 7f, targetPos.y + 1f, targetPos.z);
            var targetsBackRot = new Vector3(tokenRot.x, tokenRot.y + 180f, tokenRot.z);

            var AssasinHit = new Vector3(targetPos.x + 3f, targetPos.y - 1f, targetPos.z);
            var HitEffect = new Vector3(targetsBackRot.x - 3.5f, targetsBackRot.y - 1, targetsBackRot.z - 2.5f);
            var AssasinHitReturn = new Vector3(targetPos.x + 7f, targetPos.y + 1f, targetPos.z);

            if (crit) { targetGetDamagePos = new Vector3(targetPos.x - 2f, targetPos.y, targetPos.z); }

            targetGetDamagePos = new Vector3(targetPos.x - 0.75f, targetPos.y, targetPos.z);
            targetGetDamageRot = new Vector3(targetRot.x, targetRot.y, targetRot.z - 1.5f);
            if (firstAttack)
            {
                // �������
                tokenSequence.Append(token.transform.DOMove(getReadyPos, 0.55f));
                // ����������� �� �����
                tokenSequence.Insert(0.55f, token.transform.DOJump(targetsBack, 1, 1, 0f));
                tokenSequence.Insert(0.55f, token.transform.DORotate(targetsBackRot, 0f));

                // ������ �����
                tokenSequence.Insert(0.85f, token.transform.DOJump(AssasinHit, 1, 1, 0.3f));
                tokenSequence.Insert(1.15f, token.transform.DOMove(AssasinHitReturn, 0.4f).OnStepComplete(Kill));
                tokenSequence.Insert(1.15f, token.transform.DORotate(HitEffect, 0.15f).SetLoops(2, LoopType.Yoyo));

                //// ���� �������� �����
                targetSequence.Insert(1.15f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo));
                targetSequence.Insert(1.15f, target.transform.DORotate(targetGetDamageRot, 0.15f).SetLoops(2, LoopType.Yoyo));
            }
            else
            {
                // ������ �����
                tokenSequence.Append(token.transform.DOJump(AssasinHit, 1, 1, 0.35f));
                tokenSequence.Insert(0.35f, token.transform.DOMove(AssasinHitReturn, 0.45f).OnStepComplete(Kill));
                tokenSequence.Insert(0.35f, token.transform.DORotate(HitEffect, 0.2f).SetLoops(2, LoopType.Yoyo));

                targetSequence.Insert(0.35f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo));
                targetSequence.Insert(0.35f, target.transform.DORotate(targetGetDamageRot, 0.15f).SetLoops(2, LoopType.Yoyo));
            }
        }

        void Kill()
        {
            tokenSequence.Kill();
            targetSequence.Kill();
        }
    }
    private void MultiEnemyAnimation(GameObject token, GameObject target, bool crit, bool firstAttack)
    {
        var data = token.GetComponent<Token>();

        Sequence tokenSequence = DOTween.Sequence();
        Sequence targetSequence = DOTween.Sequence();

        var targetPos = new Vector3(target.transform.position.x, target.transform.position.y, target.transform.position.z);
        var targetRot = new Vector3(target.transform.rotation.x, target.transform.rotation.y, target.transform.rotation.z);

        // ������ ����� �� �����
        var targetGetDamagePos = new Vector3(targetPos.x + 0.75f, targetPos.y, targetPos.z);
        var targetGetDamageRot = new Vector3(targetRot.x, targetRot.y, targetRot.z - 1.5f);

        if (crit)
        {
            targetGetDamagePos = new Vector3(targetPos.x + 2f, targetPos.y, targetPos.z);
            targetGetDamageRot = new Vector3(targetRot.x, targetRot.y, targetRot.z - 2.5f);
        }


        var projectile = Instantiate(data.Projectile, token.transform);

        var waypoints = new Vector3[1];
        waypoints[0] = target.transform.position;

        var AfterArrowPosition = new Vector3(tokenPos.x - 1.25f, tokenPos.y + 3.5f, tokenPos.z);
        var AfterArrowPositionReturn = new Vector3(tokenPos.x + 0.5f, tokenPos.y + 3.5f, tokenPos.z);


        //���� �������� ����
        //����� �������, �������� ���������.
        if (firstAttack)
        {
            tokenSequence.Insert(0.8f, projectile.transform.DOPath(waypoints, 0.25f, PathType.CatmullRom).SetLookAt(0.01f)).OnComplete(KillProjectile);
            targetSequence.Insert(1.05f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo)).OnComplete(Kill);
            targetSequence.Insert(1.05f, target.transform.DORotate(targetGetDamageRot, 0.15f).SetLoops(2, LoopType.Yoyo));
        }
        else
        {
            tokenSequence.Insert(0.35f, projectile.transform.DOPath(waypoints, 0.25f, PathType.CatmullRom).SetLookAt(0.01f)).OnStepComplete(KillProjectile);
            targetSequence.Insert(0.6f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo));
            targetSequence.Insert(0.6f, target.transform.DORotate(targetGetDamageRot, 0.15f).SetLoops(2, LoopType.Yoyo)).OnStepComplete(Kill);

        }

        void KillProjectile()
        {
            if (projectile != null) { Destroy(projectile); }
        }

        void Kill()
        {

            tokenSequence.Kill();
            targetSequence.Kill();
        }




    }
    private void CrystalAnimation(GameObject token, GameObject target, bool firstAttack)
    {
        var data = token.GetComponent<Token>();

        Sequence tokenSequence = DOTween.Sequence();
        Sequence targetSequence = DOTween.Sequence();

        // ������ ����� �� �����
        var targetGetDamagePos = new Vector3(targetPos.x + 0.25f, targetPos.y, targetPos.z);
        var targetGetDamageRot = new Vector3(targetRot.x, targetRot.y, targetRot.z - 1.5f);

        if (data.WarroirType)
        {
            var targetTransformForToken = new Vector3(targetPos.x - 4f, targetPos.y, targetPos.z);
            var AfterHitPositionByWarroir = new Vector3(targetPos.x - 7f, targetPos.y, targetPos.z);
            var HitEffect = new Vector3(tokenRot.x + 3.5f, tokenRot.y - 1, tokenRot.z - 2.5f);

            if (firstAttack)
            {
                tokenSequence.Append(token.transform.DOMove(getReadyPos, 0.55f));
                tokenSequence.Insert(0.55f, token.transform.DOJump(targetTransformForToken, 3, 1, 0.25f));

                // ������ �����
                tokenSequence.Insert(0.8f, token.transform.DOMove(AfterHitPositionByWarroir, 0.45f).OnStepComplete(Kill));
                tokenSequence.Insert(0.8f, token.transform.DORotate(HitEffect, 0.15f).SetLoops(2, LoopType.Yoyo));

                // ���� �������� �����
                targetSequence.Insert(0.8f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo));
            }
            else
            {
                tokenSequence.Append(token.transform.DOJump(targetTransformForToken, 1, 1, 0.3f));
                tokenSequence.Insert(0.3f, token.transform.DOMove(AfterHitPositionByWarroir, 0.55f)).OnStepComplete(Kill);
                tokenSequence.Insert(0.3f, token.transform.DORotate(HitEffect, 0.15f).SetLoops(2, LoopType.Yoyo));

                // ���� �������� �����
                targetSequence.Insert(0.3f, target.transform.DORotate(targetGetDamageRot, 0.15f).SetLoops(2, LoopType.Yoyo));
            }
        }
        else if (data.RangedType)
        {
            var projectile = Instantiate(data.Projectile, token.transform);
            waypoints[0] = target.transform.position;

            var AfterArrowPosition = new Vector3(tokenPos.x - 1.25f, tokenPos.y + 3.5f, tokenPos.z);
            var AfterArrowPositionReturn = new Vector3(tokenPos.x + 0.5f, tokenPos.y + 3.5f, tokenPos.z);

            if (firstAttack)
            {
                tokenSequence.Append(token.transform.DOMove(getReadyPos, 0.45f));
                // ����������� 
                tokenSequence.Insert(0.45f, token.transform.DOMove(AfterArrowPosition, 0.35f));
                //����� �������, �������� ���������.
                tokenSequence.Insert(0.8f, projectile.transform.DOPath(waypoints, 0.25f, PathType.CatmullRom).SetLookAt(0.01f)).OnComplete(KillProjectile);
                //������ �� ������� �������
                tokenSequence.Insert(0.8f, token.transform.DOMove(AfterArrowPositionReturn, 0.20f));

                //���� �������� ����
                targetSequence.Insert(1.05f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo)).OnComplete(Kill);
            }
            else
            {
                tokenSequence.Append(token.transform.DOMove(AfterArrowPosition, 0.35f));

                //����� �������, �������� ���������.
                tokenSequence.Insert(0.35f, projectile.transform.DOPath(waypoints, 0.25f, PathType.CatmullRom).SetLookAt(0.01f)).OnStepComplete(KillProjectile);
                //������ �� ������� �������
                tokenSequence.Insert(0.35f, token.transform.DOMove(AfterArrowPositionReturn, 0.20f));
                //���� �������� ����
                targetSequence.Insert(0.6f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo));

            }

            void KillProjectile()
            {
                if (projectile != null) { Destroy(projectile); }
            }
        }
        else if (data.AssassinType)
        {
            var targetsBack = new Vector3(targetPos.x - 7f, targetPos.y, targetPos.z);
            var targetsBackRot = new Vector3(tokenRot.x, tokenRot.y + 180f, tokenRot.z);

            var AssasinHit = new Vector3(targetPos.x - 3f, targetPos.y, targetPos.z);
            var HitEffect = new Vector3(tokenRot.x + 3.5f, tokenRot.y - 1, tokenRot.z - 2.5f);
            var AssasinHitReturn = new Vector3(targetPos.x - 7f, targetPos.y, targetPos.z);
            if (firstAttack)
            {
                // �������
                tokenSequence.Append(token.transform.DOMove(getReadyPos, 0.55f));
                // ����������� �� �����
                tokenSequence.Insert(0.55f, token.transform.DOJump(targetsBack, 1, 1, 0f));

                // ������ �����
                tokenSequence.Insert(0.85f, token.transform.DOJump(AssasinHit, 1, 1, 0.3f));
                tokenSequence.Insert(1.15f, token.transform.DOMove(AssasinHitReturn, 0.4f).OnStepComplete(Kill));
                tokenSequence.Insert(1.15f, token.transform.DORotate(HitEffect, 0.15f).SetLoops(2, LoopType.Yoyo));

                //// ���� �������� �����
                targetSequence.Insert(1.15f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo));
            }
            else
            {
                // ������ �����
                tokenSequence.Append(token.transform.DOJump(AssasinHit, 1, 1, 0.35f));
                tokenSequence.Insert(0.35f, token.transform.DOMove(AssasinHitReturn, 0.45f).OnStepComplete(Kill));
                tokenSequence.Insert(0.35f, token.transform.DORotate(HitEffect, 0.2f).SetLoops(2, LoopType.Yoyo));


                // ���� �������� �����
                targetSequence.Insert(0.35f, target.transform.DOMove(targetGetDamagePos, 0.2f).SetLoops(2, LoopType.Yoyo));
            }
        }

        void Kill()
        {
            tokenSequence.Kill();
            targetSequence.Kill();
        }
    }
    private void AllyAnimation(GameObject token, GameObject target, bool firstAttack)
    {
        var data = token.GetComponent<Token>();
        Sequence tokenSequence = DOTween.Sequence();

        if (data.WarroirType)
        {
            var targetTransformForToken = new Vector3(targetPos.x - 4f, targetPos.y, targetPos.z);
            var AfterHitPositionByWarroir = new Vector3(targetPos.x - 7f, targetPos.y, targetPos.z);
            var HitEffect = new Vector3(tokenRot.x + 3.5f, tokenRot.y - 1, tokenRot.z - 2.5f);

            if (firstAttack)
            {
                tokenSequence.Append(token.transform.DOMove(getReadyPos, 0.55f));
                tokenSequence.Insert(0.55f, token.transform.DOJump(targetTransformForToken, 3, 1, 0.25f));

                // ������ �����
                tokenSequence.Insert(0.8f, token.transform.DOMove(AfterHitPositionByWarroir, 0.45f).OnStepComplete(Kill));
                tokenSequence.Insert(0.8f, token.transform.DORotate(HitEffect, 0.15f).SetLoops(2, LoopType.Yoyo));
            }
            else
            {
                tokenSequence.Append(token.transform.DOJump(targetTransformForToken, 1, 1, 0.3f));
                tokenSequence.Insert(0.3f, token.transform.DOMove(AfterHitPositionByWarroir, 0.55f)).OnStepComplete(Kill);
                tokenSequence.Insert(0.3f, token.transform.DORotate(HitEffect, 0.15f).SetLoops(2, LoopType.Yoyo));
            }
        }
        else if (data.RangedType)
        {
            var projectile = Instantiate(data.AllyProjectile, token.transform);
            waypoints[0] = target.transform.position;

            var AfterArrowPosition = new Vector3(tokenPos.x - 1.25f, tokenPos.y + 3.5f, tokenPos.z);
            var AfterArrowPositionReturn = new Vector3(tokenPos.x + 0.5f, tokenPos.y + 3.5f, tokenPos.z);

            if (firstAttack)
            {
                tokenSequence.Append(token.transform.DOMove(getReadyPos, 0.45f));
                // ����������� 
                tokenSequence.Insert(0.45f, token.transform.DOMove(AfterArrowPosition, 0.35f));
                //����� �������, �������� ���������.
                tokenSequence.Insert(0.8f, projectile.transform.DOPath(waypoints, 0.25f, PathType.CatmullRom).SetLookAt(0.01f)).OnComplete(KillProjectile);
                //������ �� ������� �������
                tokenSequence.Insert(0.8f, token.transform.DOMove(AfterArrowPositionReturn, 0.20f));
            }
            else
            {
                tokenSequence.Append(token.transform.DOMove(AfterArrowPosition, 0.35f));
                //����� �������, �������� ���������.
                tokenSequence.Insert(0.35f, projectile.transform.DOPath(waypoints, 0.25f, PathType.CatmullRom).SetLookAt(0.01f)).OnStepComplete(KillProjectile);
                //������ �� ������� �������
                tokenSequence.Insert(0.35f, token.transform.DOMove(AfterArrowPositionReturn, 0.20f));
            }

            void KillProjectile()
            {
                if (projectile != null) { Destroy(projectile); }
            }
        }
        else if (data.AssassinType)
        {
            var targetsBack = new Vector3(targetPos.x + 7f, targetPos.y + 1f, targetPos.z);
            var targetsBackRot = new Vector3(tokenRot.x, tokenRot.y + 180f, tokenRot.z);

            var AssasinHit = new Vector3(targetPos.x + 3f, targetPos.y - 1f, targetPos.z);
            var HitEffect = new Vector3(targetsBackRot.x - 3.5f, targetsBackRot.y - 1, targetsBackRot.z - 2.5f);
            var AssasinHitReturn = new Vector3(targetPos.x + 7f, targetPos.y + 1f, targetPos.z);

            if (firstAttack)
            {
                // �������
                tokenSequence.Append(token.transform.DOMove(getReadyPos, 0.55f));
                // ����������� �� �����
                tokenSequence.Insert(0.55f, token.transform.DOJump(targetsBack, 1, 1, 0f));
                tokenSequence.Insert(0.55f, token.transform.DORotate(targetsBackRot, 0f));

                // ������ �����
                tokenSequence.Insert(0.85f, token.transform.DOJump(AssasinHit, 1, 1, 0.3f));
                tokenSequence.Insert(1.15f, token.transform.DOMove(AssasinHitReturn, 0.4f).OnStepComplete(Kill));
                tokenSequence.Insert(1.15f, token.transform.DORotate(HitEffect, 0.15f).SetLoops(2, LoopType.Yoyo));
            }
            else
            {
                tokenSequence.Append(token.transform.DOJump(AssasinHit, 1, 1, 0.35f));
                tokenSequence.Insert(0.35f, token.transform.DOMove(AssasinHitReturn, 0.45f).OnStepComplete(Kill));
                tokenSequence.Insert(0.35f, token.transform.DORotate(HitEffect, 0.2f).SetLoops(2, LoopType.Yoyo));
            }
        }

        void Kill()
        {
            tokenSequence.Kill();
        }
    }


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

            if (!evasion) { ApplyDamageBySingle(token, target, crit); }
            else
            {
                targetOverride.WhenEvaded();
                ShowMiss?.Invoke(target);
            }


            if (!targetHelper.Frostbite)
            {
                if (targetHelper.CounterAttackMelee && tokenData.WarroirType ||
                    targetHelper.CounterAttackRange && tokenData.RangedType)
                {
                    if (targetData.AttackPossible || targetData.CurrentAttack > 0)
                    {
                        CounterAttack(target, token, crit);
                    }
                }
            }
            CheckForDeath(token, target);
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
            if (!targetHelper.IsDead) { targetOverride.WhenFirstAttacked(token); AddEnergyInAttack?.Invoke(target); }
            if (!tokenHelper.IsDead) { tokenOverride.FirstAttack(target); if (!tokenHelper.AbilityAttacked) { AddEnergyInAttack?.Invoke(token); } }
        }

        var currentAttacks = (tokenData.AttacksCount - 1) + tokenHelper.AttacksCountAdds;
        tokenHelper.AttacksCountAdds = 0;

        // ���� �� ��� �����?
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
    private IEnumerator FirstCrystalAttack(GameObject token, GameObject target)
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

        enemyCrystal.ApplyDamage(tokenData.CurrentAttack, headCenter.RedPlayer);

        if (!tokenHelper.IsDead) { tokenOverride.FirstAttack(target); if (!tokenHelper.AbilityAttacked) { AddEnergyInAttack?.Invoke(token); } }

        var currentAttacks = (tokenData.AttacksCount - 1) + tokenHelper.AttacksCountAdds;
        tokenHelper.AttacksCountAdds = 0;

        // ���� �� ��� �����?
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

            if (enemy)
            {
                EnemyAttackHelper(token, target, firstAttack);
            }
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
                ApplyDamageBySingle(token, target, crit);
                CheckForDeath(token, target);
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
            enemyCrystal.ApplyDamage(attack, headCenter.RedPlayer); 
        }
    }


    private void CheckForDeath(GameObject token, GameObject target)
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
    private void EndAttack(GameObject token, GameObject target)
    {
        var tokenHelper = token.GetComponent<TokenHelper>();

        if (!tokenHelper.IsDead) { EndAnimation(token); }
        if (tokenHelper.AbilityAttacked) { var tokenOverride = token.GetComponent<TokenOverride>(); tokenOverride.AbilityAttackEnd(); }


        if (enemy || ally)
        {
            var targetHelper = target.GetComponent<TokenHelper>();
            if (!targetHelper.IsDead) { targetHelper.WhenTargetLight.enabled = false; targetHelper.Animated = false; }
        }
        else
        {
            enemyCrystal.WhenTargetForAttack.gameObject.SetActive(false);
            enemyCrystal.CrystalCanBeAttacked = false;
        }
    }
    private void EndAnimation(GameObject token)
    {
        var data = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();

        if (!tokenHelper.IsDead) { StartCoroutine(DelayAnimated(data, tokenHelper)); }

        if (data.WarroirType) { token.transform.DOJump(tokenPos, 1, 1, 0.35f); }
        else if (data.RangedType) { token.transform.DOMove(tokenPos, 0.55f); }
        else if (data.AssassinType)
        {
            token.transform.DOMove(getReadyPos, 0.0f).OnStepComplete(GetSpot);
            token.transform.DORotate(tokenRot, 0f);

            void GetSpot()
            {
                token.transform.DOMove(tokenPos, 0.55f);
            }
        }

    }
    private IEnumerator DelayAnimated(Token data, TokenHelper tokenHelper)
    {

        if (data.WarroirType) { yield return new WaitForSeconds(0.35f); }
        else { yield return new WaitForSeconds(0.55f); }
        tokenHelper.Animated = false;

    }



    private void ApplyDamageBySingle(GameObject token, GameObject target, bool crit)
    {
        var tokenData = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        var tokenOverride = token.GetComponent<TokenOverride>();

        var targetData = target.GetComponent<Token>();
        var targetHelper = target.GetComponent<TokenHelper>();
        var targetOverride = target.GetComponent<TokenOverride>();

        tokenOverride.WhenAttacks(target);

        var ctitATK = tokenData.CriticalDMG;
        //int dmgReduce = 1;
        //if (tokenHelper.DMGReduce > 0) { dmgReduce = 100 / tokenHelper.DMGReduce; }

        //Debug.Log("��� �������� � ����� " + tokenHelper.ATKAdds);
        var currentDamage = ((tokenData.CurrentAttack + tokenHelper.ATKAdds) - targetData.CurrentDefence) - targetHelper.DMGReduce;

        tokenHelper.ATKAdds = 0;
        var critdamage = 0.0f;

        if (currentDamage > 0)
        {
            
            if (tokenHelper.Desolator) { currentDamage = tokenData.CurrentAttack + tokenHelper.ATKAdds; }

            if (crit)
            {
                tokenOverride.CriticalAttack(target);
                // ����������, AUTO ������� � ������� �������
                critdamage = currentDamage * ctitATK;
                if (critdamage > 1 && critdamage < 2) { critdamage = 2; }
                else if (critdamage > 2 && critdamage < 3) { critdamage = 3; }

                currentDamage = (int)critdamage;
            }


        }
        else { currentDamage = 0; }



        List<EffectData> damageEffects = new();

        targetData.CurrentHealth -= currentDamage;
        ApplyFeatureEffects(token, target, currentDamage, damageEffects); 
    
        targetOverride.WhenAttacked();

        if (!crit) { target.GetComponent<AudioSource>().PlayOneShot(arrowHit); }
        else { target.GetComponent<AudioSource>().PlayOneShot(critHit); }

        if (targetHelper.Protected)
        {
            var protectorCrit = false;
            var protector = targetHelper.Protector;
            var protectorData = protector.GetComponent<Token>();
            var currentProtectorDamage = targetHelper.DMGReduce - protectorData.CurrentDefence;

            protectorData.CurrentHealth -= currentProtectorDamage;

            ShowDamage?.Invoke(protector, currentProtectorDamage, protectorCrit);
            RefreshStat?.Invoke(protector);
        }


        RefreshStat?.Invoke(target);
        RefreshStat?.Invoke(token);

        ShowDamageEffects?.Invoke(target, damageEffects, currentDamage, crit);
    }

    private void ApplyDamageByMulti(GameObject token, GameObject target, bool crit)
    {
        var tokenData = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        var tokenOverride = token.GetComponent<TokenOverride>();

        var targetData = target.GetComponent<Token>();
        var targetHelper = target.GetComponent<TokenHelper>();
        var targetOverride = target.GetComponent<TokenOverride>();

        tokenOverride.WhenAttacks(target);

        var ctitATK = tokenData.CriticalDMG;
        int dmgReduce = 1;
        if (tokenHelper.DMGReduce > 0) { dmgReduce = 100 / tokenHelper.DMGReduce; }


        var currentDamage = (((tokenData.CurrentAttack + tokenHelper.ATKAdds) - targetData.CurrentDefence) / dmgReduce) - targetHelper.DMGReduce;

        tokenHelper.ATKAdds = 0;
        var critdamage = 0.0f;

        if (currentDamage > 0)
        {
            if (tokenHelper.Desolator) { currentDamage = tokenData.CurrentAttack + tokenHelper.ATKAdds; }

            if (crit)
            {
                tokenOverride.CriticalAttack(target);
                // AUTO ������� � ������� �������
                critdamage = currentDamage * ctitATK;
                if (critdamage > 1 && critdamage < 2) { critdamage = 2; }
                else if (critdamage > 2 && critdamage < 3) { critdamage = 3; }

                currentDamage = (int)critdamage;
            }


        }
        else { currentDamage = 0; }



        List<EffectData> damageEffects = new();

        targetData.CurrentHealth -= currentDamage;

 
        targetOverride.WhenAttacked();

        if (!crit) { target.GetComponent<AudioSource>().PlayOneShot(arrowHit); }
        else { target.GetComponent<AudioSource>().PlayOneShot(critHit); }

        if (targetHelper.Protected)
        {
            var protectorCrit = false;
            var protector = targetHelper.Protector;
            var protectorData = protector.GetComponent<Token>();
            var currentProtectorDamage = targetHelper.DMGReduce - protectorData.CurrentDefence;

            protectorData.CurrentHealth -= currentProtectorDamage;

            ShowDamage?.Invoke(protector, currentProtectorDamage, protectorCrit);
            RefreshStat?.Invoke(protector);
        }


        RefreshStat?.Invoke(target);
        RefreshStat?.Invoke(token);

        ShowDamageEffects?.Invoke(target, damageEffects, currentDamage, crit);
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
            ShowDamageEffect(token, targetHelper.SpikeEffect);
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
        //Debug.Log("�������� ����� � ������� �� � " + tokenData.TokenName);
        tokenData.CurrentHealth -= spikeDamage;
        RefreshStat?.Invoke(whoAttacks);
    }



    private bool CriticalAttack(GameObject token)
    {
        var data = token.GetComponent<Token>();
        var tokenHelper = token.GetComponent<TokenHelper>();
        //Debug.Log("�������� �� ����, ��� �������� �� ����� " + tokenHelper.CriticalChanceAdds);
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

        if (data.Evasion > 0)
        {
            if (evasionNumber <= evasionChance) { evasion = true; }
        }
        return evasion;
    }



    private void StartAttackHelper(GameObject token, GameObject target)
    {
        var tokenHelper = token.GetComponent<TokenHelper>();
        tokenHelper.CanAttack = false;
        tokenHelper.Attacked = true;

        SetTarget(target);
        InitTransforms(token, target);
    }
    private void InitTransforms(GameObject token, GameObject target)
    {
        getReadyPos = new Vector3(token.transform.position.x + 1, token.transform.position.y + 3.5f, token.transform.position.z);

        tokenPos = new Vector3(token.transform.position.x, token.transform.position.y, token.transform.position.z);
        tokenRot = new Vector3(token.transform.rotation.x, token.transform.rotation.y, token.transform.rotation.z);

        targetPos = new Vector3(target.transform.position.x, target.transform.position.y, target.transform.position.z);
        targetRot = new Vector3(target.transform.rotation.x, target.transform.rotation.y, target.transform.rotation.z);
    }
    private void SetTarget(GameObject target)
    {
        StopAttack = false;
        enemy = false;
        crystalHeart = false;
        ally = false;

        if (target.GetComponent<RedPlayer>()) { enemy = true; }
        else if (target.GetComponent<CrystalHeart>()) { crystalHeart = true; }
        else if (target.GetComponent<BluePlayer>()) { ally = true; }
    }



    // �������� ����������� ��, ������
    public void RefershStats(GameObject token)
    {
        RefreshStat?.Invoke(token);
    }
    public void ShowTokenDamage(GameObject token, List<EffectData> effects, int value, bool crit)
    {
        ShowDamageEffects?.Invoke(token, effects, value, crit);
    }
    public void ShowEffectByAI(GameObject token, EffectData effect)
    {
        ShowEffect?.Invoke(token, effect, effect.Buff);
    }
    public void ShowDamageAI(GameObject token, int value, bool crit)
    {
        ShowDamage?.Invoke(token, value, crit);

    }
    public void ShowMissAi(GameObject token)
    {
        var tokenOverride = token.GetComponent<TokenOverride>();
        tokenOverride.WhenEvaded();
        ShowMiss?.Invoke(token);

    }
    public void AddEnergy(GameObject token)
    {
        AddEnergyInAttack?.Invoke(token);
    }



    private void InitHelper()
    {
        headCenter = FindObjectOfType<HeadCenter>();
        players = FindObjectOfType<Players>();
        turnSystem = FindObjectOfType<TurnSystemCenter>();
        enemyCrystal = headCenter.RedPlayer.BattleData.Crystal;

        waypoints = new Vector3[1];
    }
}