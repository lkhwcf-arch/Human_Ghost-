using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class CombatChecks
{
    public static int Run(Player definition)
    {
        int checks = 0;
        void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
        void Expect<T>(Action action) where T : Exception
        { try { action(); } catch (T) { checks++; return; } throw new Exception("예외 누락 " + typeof(T).Name); }
        AttackSpec[] Specs() => new[]
        {
            new AttackSpec(120,80,250,200,0,10,12), new AttackSpec(120,80,250,200,0,10,12),
            new AttackSpec(200,100,400,0,10,20,12), new AttackSpec(100,80,200,0,-2,10,12)
        };
        PlayerActor Actor(int stamina = -1, bool grounded = true) => new PlayerActor(definition, Specs(), grounded, stamina < 0 ? definition.StaminaMax : stamina);
        BossPoise Boss()
        {
            var common = new UnitStat(1006,0,"Boss",null,420,28,4,300,500,14,120,250,0);
            var phase = new BossState(4001,1006,1500,1500,2000,15,2000,"DAMAGE",1800,false,0,"Idle",10000);
            return new BossPoise(new Boss(common,new[]{phase}),phase);
        }

        var actor = Actor();
        actor.SetMove(1);
        int start = actor.Stamina.Current;
        Check(actor.TryAttack() && actor.CurrentState == StateKey.Attack1 && actor.Stamina.Current == start - 12, "1타와 공격 비용");
        Check(actor.Locomotion.HorizontalSpeed == 0 && !actor.TryJump(), "공격 중 이동·점프 잠금");
        actor.SetMove(-1);
        Check(actor.CurrentState == StateKey.Attack1, "이동 입력이 공격 상태를 덮지 않음");
        Check(!actor.TryRoll() && actor.Stamina.Current == start - 12, "선딜 구르기 거부는 소모 없음");
        actor.Tick(119);
        Check(!actor.IsAttackActive, "선딜 경계 이전");
        actor.Tick(1);
        Check(actor.IsAttackActive && !actor.TryRoll(), "판정 시작과 판정 중 캔슬 거부");
        Check(actor.TryRegisterTarget(7) && !actor.TryRegisterTarget(7) && actor.TryRegisterTarget(8), "판정 창 대상 중복 피해 방지");
        actor.Tick(80);
        Check(!actor.IsAttackActive && !actor.TryRegisterTarget(9), "판정 종료 뒤 대상 등록 거부");
        Check(actor.TryAttack(), "1타 후딜 콤보 입력");
        actor.Tick(250);
        Check(actor.CurrentState == StateKey.Attack2 && actor.Stamina.Current == start - 24, "2타 진입 시에만 소모");
        actor.Tick(200);
        Check(actor.TryAttack(), "2타 콤보 입력");
        actor.Tick(250);
        Check(actor.CurrentState == StateKey.Attack3 && actor.Stamina.Current == start - 36, "3타 연결");
        Check(!actor.TryAttack(), "3타 뒤 추가 콤보 없음");
        actor.Tick(700);
        Check(actor.CurrentState == StateKey.Walk && actor.Locomotion.HorizontalSpeed == -definition.MoveSpeed, "콤보 종료 후 현재 이동 복구");

        var expired = Actor(); expired.TryAttack(); expired.Tick(400);
        Check(!expired.TryAttack(), "콤보 창 끝은 배타적");
        expired.Tick(50);
        Check(expired.TryAttack() && expired.CurrentState == StateKey.Attack1, "창을 놓친 뒤 1타부터");
        var buffered = Actor(); buffered.TryAttack(); buffered.Tick(49);
        Check(!buffered.TryAttack(), "선입력 버퍼 밖 입력 거부");
        buffered.Tick(1);
        Check(buffered.TryAttack(), "InputBuffer 경계의 선입력");
        buffered.Tick(400);
        Check(buffered.CurrentState == StateKey.Attack2, "유효 선입력이 다음 타로 연결");

        var poor = Actor(11);
        Check(!poor.TryAttack() && poor.Stamina.Current == 11 && poor.CurrentState == StateKey.Idle, "부족한 공격 입력은 무변화");
        var poorChain = Actor(17); poorChain.TryAttack(); poorChain.Tick(200); poorChain.TryAttack(); poorChain.Tick(250);
        Check(poorChain.CurrentState == StateKey.Idle && poorChain.Stamina.Current == 5, "다음 타 비용 부족 시 콤보 종료");
        var air = Actor(grounded:false);
        Check(air.TryAttack() && air.CurrentAttack.ExtraDamage == -2 && !air.TryAttack(), "공중 공격은 1타만");
        air.Tick(380);
        Check(air.CurrentState == StateKey.Jump && !air.TryJump(), "공중 공격 종료도 접지 가정 없음");

        var roll = Actor(); roll.TryAttack(); roll.Tick(200); roll.TryAttack();
        int cost = roll.Stamina.Current;
        Check(roll.TryRoll() && roll.CurrentState == StateKey.Roll && roll.Stamina.Current == cost - definition.RollStamina, "후딜 구르기 캔슬·비용");
        Check(roll.IsInvulnerable && !roll.ReceiveHit(10), "구르기 시작 무적");
        roll.Tick(definition.RollIFrame-1);
        Check(roll.IsInvulnerable, "구르기 무적 끝 이전");
        roll.Tick(1);
        Check(!roll.IsInvulnerable && roll.ReceiveHit(10) && roll.CurrentState == StateKey.Hit, "구르기 남은 구간 피격 가능");
        Check(roll.Health == definition.HP-10 && !roll.ReceiveHit(10), "HP 적용과 피격 무적 중 중복 거부");
        roll.Tick(definition.HitIFrame);
        Check(roll.CurrentState == StateKey.Idle && roll.ReceiveHit(10,true) && roll.CurrentState == StateKey.CritHit, "피격 회복·치명타 경직");
        roll.Tick(definition.CritHitStunTime);
        Check(roll.CurrentState == StateKey.Idle, "치명타 경직 종료");
        roll.Tick(definition.HitIFrame);
        Check(roll.ReceiveHit(int.MaxValue) && roll.IsDead && roll.CurrentState == StateKey.Dead, "치명 피해는 Dead 우선");
        Check(!roll.TryAttack() && !roll.TryRoll() && !roll.TryJump(), "사망 행동 잠금");
        int deadStamina = roll.Stamina.Current; roll.Tick(int.MaxValue);
        Check(roll.Stamina.Current == deadStamina, "사망 뒤 런타임 진행 중단");
        var huge = Actor(); huge.TryAttack(); huge.Tick(int.MaxValue);
        Check(huge.CurrentState == StateKey.Idle && huge.Stamina.Current == definition.StaminaMax, "큰 Tick이 경계를 통과하고 정상 종료");
        var events = Actor(); int activeEvents=0; events.AttackWindowChanged += x=>activeEvents++;
        events.TryAttack(); events.Tick(200); events.Tick(250);
        Check(activeEvents==2, "판정 시작·종료 각 한 번");

        var boss = Boss(); boss.ReceiveHit(1,120);
        Check(boss.IsExposed && boss.Health == 419, "보스 체세0 노출");
        boss.Tick(1999); Check(boss.IsExposed, "노출 끝 이전");
        boss.Tick(1); Check(!boss.IsExposed && boss.Poise==120, "기회를 놓치면 체세 전량 회복");
        boss.ReceiveHit(1,30); boss.Tick(2000);
        Check(boss.Poise==90, "체세 회복 딜레이 경계");
        boss.Tick(1000); Check(boss.Poise==105, "체세 초당 회복");
        boss.Tick(int.MaxValue); Check(boss.Poise==120, "체세 최대치·큰 Tick");
        var first = Boss(); first.ReceiveHit(1,60);
        var second = Boss(); second.ReceiveHit(1,60);
        first.Tick(3999); for(int i=0;i<3999;i++)second.Tick(1);
        Check(first.Poise==second.Poise, "체세 정수 나머지 분할 보존");
        var deadBoss = Boss(); deadBoss.ReceiveHit(420,120);
        Check(deadBoss.IsDead && deadBoss.CurrentState==StateKey.Dead && !deadBoss.TryBeginExecution(), "HP0이 체세0보다 우선");

        var target = Boss(); target.ReceiveHit(1,120);
        var executor = Actor();
        Check(!executor.TryExecute(target,false) && target.IsExposed, "거리 조건은 호출자가 명시");
        Check(executor.TryExecute(target,true) && executor.IsInvulnerable && target.IsInvulnerable, "해체 중 양쪽 무적");
        Check(!target.ReceiveHit(5,5) && !executor.ReceiveHit(5), "해체 연출 중 일반 피격 거부");
        executor.Tick(int.MaxValue); target.Tick(int.MaxValue);
        Check(target.Health==419 && target.IsInvulnerable, "연출 완료 신호 없이 해체 자동 완료하지 않음");
        Check(executor.CompleteExecution(true) && target.Health==344 && target.Poise==120, "최대HP18% 정수 피해와 명시적 회복");
        Check(!executor.CompleteExecution(true) && !target.CompleteExecution(true), "해체 완료 중복 피해 방지");
        target.ReceiveHit(1,120); target.TryBeginExecution(); target.CompleteExecution(false);
        Check(target.IsExposed && target.Poise==0, "회복하지 않는 종료 정책도 호출자가 명시");
        Check(CombatMath.Damage(definition,target.Definition,10,true)==24, "기획 정수 치명 피해 예시");
        Check(CombatMath.Damage(definition,target.Definition,6,false)==14, "기획 스킬 추가 피해 예시");

        var striker=Actor();var hitTarget=Boss();striker.TryAttack();striker.Tick(120);
        Check(striker.TryHitBoss(1,hitTarget,false,false) && hitTarget.Health==412 && hitTarget.Poise==110, "공격 창에서 보스 HP·체세 동시 적용");
        Check(!striker.TryHitBoss(1,hitTarget,false,true) && hitTarget.Health==412, "같은 공격 창은 중복 치명 피해도 거부");
        var dodgedTarget=Boss();
        Check(!striker.TryHitBoss(2,dodgedTarget,true,true) && dodgedTarget.Health==420 && dodgedTarget.Poise==120, "회피하면 HP·체세 모두 무피해");

        Expect<ArgumentOutOfRangeException>(()=>new AttackSpec(0,1,0,0,0,0,0));
        Expect<ArgumentOutOfRangeException>(()=>new AttackSpec(1,1,0,1,0,0,0));
        Expect<OverflowException>(()=>new AttackSpec(int.MaxValue,1,0,0,0,0,0));
        Expect<ArgumentException>(()=>new PlayerActor(definition,new AttackSpec[0],true,0));
        Expect<ArgumentOutOfRangeException>(()=>actor.Tick(-1));
        Expect<ArgumentOutOfRangeException>(()=>actor.ReceiveHit(-1));
        Expect<ArgumentOutOfRangeException>(()=>actor.TryRegisterTarget(0));
        Expect<ArgumentOutOfRangeException>(()=>boss.Tick(-1));
        return checks;
    }
}
