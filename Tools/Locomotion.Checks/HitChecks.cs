using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

// 확률 타격(회피→치명) 순서·난수 소비 횟수·경계 검사입니다.
internal static class HitChecks
{
    public static int Run(Player player, Monster jigegwi, Func<MonsterActor> husugwi, Func<MonsterActor> husugwiGrab)
    {
        int checks = 0;
        void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
        void Expect<T>(Action action) where T : Exception
        { try { action(); } catch (T) { checks++; return; } throw new Exception("예외 누락 " + typeof(T).Name); }

        int calls = 0;
        Func<int, int, int> Script(params int[] values)
        {
            var queue = new Queue<int>(values); calls = 0;
            return (min, max) =>
            {
                if (min != 0 || max != 10000) throw new Exception("확률 난수 범위");
                calls++;
                return queue.Dequeue();
            };
        }
        // 1타: 선딜120/판정80, 추가 피해는 지정값.
        PlayerActor Attacking(int extra = 0, int poise = 10)
        {
            var actor = new PlayerActor(player, new[]
            {
                new AttackSpec(120,80,250,200,extra,poise,12), new AttackSpec(120,80,250,200,0,8,12),
                new AttackSpec(120,80,250,200,0,8,12), new AttackSpec(200,100,400,0,10,16,12),
                new AttackSpec(100,80,200,0,-2,8,12)
            }, true, player.StaminaMax);
            actor.TryAttack(); actor.Tick(120);
            return actor;
        }
        BossPoise Boss(int dodge = 300, int crit = 500)
        {
            var common = new UnitStat(1006,0,"Boss",null,420,28,4,dodge,crit,14,120,250,0);
            var phase = new BossState(4001,1006,1500,1500,2000,15,2000,"DAMAGE",1800,false,0,"Idle",10000);
            return new BossPoise(new Boss(common,new[]{phase}),phase);
        }
        MonsterActor Jigegwi() => new MonsterActor(jigegwi, new[] { new MonsterPattern(StateKey.Attack1, 150, new AttackSpec(500, 100, 500, 0, 0, 0, 0)) }, 300, 500, (min, max) => min);
        Check(player.Dodge == 500 && player.Crit == 1200 && player.CritDamage == 6 && player.ATK == 12 && jigegwi.Dodge == 400 && jigegwi.DEF == 2, "배포 회피·치명 정의");

        // 플레이어→일반몹: 상대 Dodge 400, 공격자 Crit 1200. 성공 조건은 난수 < 확률.
        var j = Jigegwi();
        var p = Attacking();
        Check(p.TryHitMonster(1, j, Script(400, 1200)) == HitOutcome.Hit && calls == 2 && j.Health == 40, "p는 실패: 회피·치명 1회씩, 평타 10");
        Check(p.TryHitMonster(1, j, Script(0, 0)) == HitOutcome.None && calls == 0 && j.Health == 40, "같은 창 중복 대상은 난수 0회");
        j = Jigegwi(); p = Attacking();
        Check(p.TryHitMonster(1, j, Script(400, 1199)) == HitOutcome.Critical && calls == 2 && j.Health == 34, "치명 p-1 성공, 치명 16");
        j = Jigegwi(); p = Attacking();
        Check(p.TryHitMonster(1, j, Script(399)) == HitOutcome.Dodged && calls == 1 && j.Health == 50 && j.CurrentState == StateKey.Idle, "회피 p-1 성공: 치명 난수 0회·무피해");
        Check(p.TryHitMonster(1, j, Script(9999, 9999)) == HitOutcome.None && calls == 0 && j.Health == 50, "회피한 대상 재접촉은 재추첨 없음");
        Check(p.TryHitMonster(2, j, Script(9999, 9999)) == HitOutcome.Hit && calls == 2 && j.Health == 40, "다른 대상 ID는 같은 창에서 별도 판정");
        var idle = new PlayerActor(player, new[] { new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0) }, true, player.StaminaMax);
        Check(idle.TryHitMonster(1, Jigegwi(), Script()) == HitOutcome.None && calls == 0, "비판정 상태는 난수 0회");
        var dead = Jigegwi(); dead.ReceiveHit(50);
        Check(Attacking().TryHitMonster(1, dead, Script()) == HitOutcome.None && calls == 0, "사망 대상은 난수 0회");

        // 플레이어→보스: DEF4, 추가 피해 +10 → 일반 18·치명 24, 체세 피해 20.
        var boss = Boss();
        Check(Attacking(10, 20).TryHitBoss(9, boss, Script(300, 1200)) == HitOutcome.Hit && boss.Health == 402 && boss.Poise == 100, "보스 일반 18·체세 20");
        boss = Boss();
        Check(Attacking(10, 20).TryHitBoss(9, boss, Script(300, 0)) == HitOutcome.Critical && boss.Health == 396 && boss.Poise == 100, "보스 치명 24");
        boss = Boss();
        Check(Attacking(10, 20).TryHitBoss(9, boss, Script(299)) == HitOutcome.Dodged && calls == 1 && boss.Health == 420 && boss.Poise == 120, "보스 회피: HP·체세 피해 0");
        boss = Boss(); boss.ReceiveHit(0, 120); boss.TryBeginExecution();
        Check(boss.IsInvulnerable && Attacking().TryHitBoss(9, boss, Script()) == HitOutcome.None && calls == 0, "해체 무적은 회피보다 먼저·난수 0회");
        Check(Attacking().TryHitBoss(9, Boss(0, 1200), Script(0, 9999)) == HitOutcome.Hit && calls == 2, "확률 0은 난수 0에서도 실패");
        Check(Attacking().TryHitBoss(9, Boss(10000), Script(9999)) == HitOutcome.Dodged && calls == 1, "확률 10000은 난수 9999에서도 성공");

        // 잘못된 입력은 피해·창 등록 전에 거부합니다.
        p = Attacking(); boss = Boss(10001);
        Expect<ArgumentOutOfRangeException>(() => p.TryHitBoss(9, boss, Script(0, 0)));
        Expect<ArgumentOutOfRangeException>(() => p.TryHitBoss(9, Boss(-1), Script(0, 0)));
        Check(calls == 0 && p.TryHitBoss(9, Boss(), Script(9999, 9999)) == HitOutcome.Hit, "확률 범위 거부는 창 등록을 소비하지 않음");
        boss = Boss();
        Expect<InvalidOperationException>(() => Attacking().TryHitBoss(9, boss, Script(10000)));
        Expect<InvalidOperationException>(() => Attacking().TryHitBoss(9, boss, Script(-1)));
        Check(boss.Health == 420 && boss.Poise == 120, "범위 밖 난수는 피해 미적용");
        Expect<ArgumentNullException>(() => Attacking().TryHitBoss(9, Boss(), (Func<int, int, int>)null));
        Expect<ArgumentNullException>(() => Attacking().TryHitMonster(1, null, Script()));
        Expect<ArgumentOutOfRangeException>(() => Attacking().TryHitBoss(0, Boss(), Script()));

        // 일반몹→플레이어: 플레이어 Dodge 500, 허수귀 Crit 0 → 치명 불가, 피해 20.
        var h = husugwi(); h.Observe(1, 150, -1); h.Tick(400);
        var target = new PlayerActor(player, new[] { new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0) }, true, player.StaminaMax);
        Check(h.IsAttackActive && h.TryHitPlayer(2001, target, Script(500, 0)) == HitOutcome.Hit && calls == 2 && target.Health == 80, "몬스터 공격: 회피 실패·Crit 0은 난수 0에서도 실패");
        var second = husugwi(); second.Observe(1, 150, -1); second.Tick(400);
        Check(target.IsInvulnerable && second.TryHitPlayer(2001, target, Script()) == HitOutcome.None && calls == 0, "피격 무적 중 다른 몬스터 타격은 난수 0회");
        var dodger = husugwi(); dodger.Observe(1, 150, -1); dodger.Tick(400);
        var fresh = new PlayerActor(player, new[] { new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0) }, true, player.StaminaMax);
        Check(dodger.TryHitPlayer(2001, fresh, Script(499)) == HitOutcome.Dodged && calls == 1 && fresh.Health == 100 && fresh.CurrentState == StateKey.Idle, "플레이어 회피 성공");
        var g = husugwiGrab(); g.Observe(1, 150, 1); g.Tick(500);
        var grabbed = new PlayerActor(player, new[] { new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0) }, true, player.StaminaMax);
        Check(g.TryHitPlayer(2001, grabbed, Script(500)) == HitOutcome.Hit && calls == 1 && grabbed.CurrentState == StateKey.Bound, "잡기는 회피만 판정·치명 난수 0회");
        Expect<ArgumentNullException>(() => husugwi().TryHitPlayer(2001, fresh, (Func<int, int, int>)null));

        // 같은 시드는 같은 결과(개체별 난수 소유).
        string Sequence(int seed)
        {
            var random = new Random(seed); var result = new System.Text.StringBuilder();
            for (int i = 0; i < 20; i++) { var b = Boss(); result.Append((int)Attacking().TryHitBoss(9, b, random.Next)).Append(b.Health); }
            return result.ToString();
        }
        Check(Sequence(42) == Sequence(42), "같은 시드·입력은 같은 판정");
        return checks;
    }
}
