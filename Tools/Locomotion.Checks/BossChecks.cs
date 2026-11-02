using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

// 보스 기본 패턴(내려찍기·2연타) 실행·판정 창·우선순위 검사입니다. 판정/후딜/간격은 기획 미공급이라 검사 전용 명시 값입니다.
internal static class BossChecks
{
    public static int Run(Player player)
    {
        int checks = 0;
        void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
        void Expect<T>(Action action) where T : Exception
        { try { action(); } catch (T) { checks++; return; } throw new Exception("예외 누락 " + typeof(T).Name); }

        int calls = 0;
        Func<int, int, int> Script(params int[] values)
        {
            var queue = new Queue<int>(values); calls = 0;
            return (min, max) => { calls++; return queue.Dequeue(); };
        }
        Func<int, int, int> never = (min, max) => throw new Exception("판정 거부 시 난수 소비");
        BossPoise Boss()
        {
            var common = new UnitStat(1006,0,"Boss",null,420,28,4,300,500,14,120,250,0);
            var phase = new BossState(4001,1006,1500,1500,2000,15,2000,"DAMAGE",1800,false,0,"Idle",10000);
            return new BossPoise(new Boss(common,new[]{phase}),phase);
        }
        // 기획 시작값: 내려찍기 선딜700·추가+6, 휘두르기 예비500·추가0 2회. 나머지는 검사 전용.
        BossPattern[] Patterns() => new[]
        {
            new BossPattern(StateKey.Attack1, new[] { new AttackSpec(700, 100, 400, 0, 6, 0, 0) }),
            new BossPattern(StateKey.Attack2, new[] { new AttackSpec(500, 100, 0, 0, 0, 0, 0), new AttackSpec(200, 100, 400, 0, 0, 0, 0) }),
            new BossPattern(StateKey.Attack2, new[] { new AttackSpec(500, 100, 0, 0, 0, 0, 0), new AttackSpec(700, 100, 400, 0, 0, 0, 0) })
        };
        int waitMin = -1, waitMax = -1;
        BossCombat Combat(BossPoise boss) => new BossCombat(boss, Patterns(), (min, max) => { waitMin = min; waitMax = max; return min; });
        PlayerActor Target() => new PlayerActor(player, new[] { new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0), new AttackSpec(1,1,0,0,0,0,0) }, true, player.StaminaMax);
        Check(player.HitIFrame == 600, "배포 피격 무적 600");

        // 패턴1 내려찍기: 단일 창, 보스 ATK28 + 6 - DEF0 = 34, 치명 +14.
        var boss = Boss(); var combat = Combat(boss); var p = Target();
        Check(combat.TryStart(0) && boss.CurrentState == StateKey.Attack1 && combat.HitIndex == 0, "내려찍기 시작");
        Check(!combat.TryStart(1), "실행 중 다른 패턴 거부");
        combat.Tick(699);
        Check(!combat.IsAttackActive && combat.TryHitPlayer(2001, p, never) == HitOutcome.None, "선딜 끝 이전 비판정·난수 0회");
        combat.Tick(1);
        Check(combat.IsAttackActive && combat.TryHitPlayer(2001, p, Script(500, 500)) == HitOutcome.Hit && calls == 2 && p.Health == 66, "선딜 경계 판정·피해 34");
        Check(combat.TryHitPlayer(2001, Target(), never) == HitOutcome.None, "피격 무적 대상이 아니어도 같은 대상 ID 중복 0");
        combat.Tick(99);
        Check(combat.IsAttackActive, "판정 끝 이전");
        combat.Tick(1);
        Check(!combat.IsAttackActive && combat.TryHitPlayer(7, Target(), never) == HitOutcome.None, "판정 끝은 배타적");
        combat.Tick(400);
        Check(boss.CurrentState == StateKey.Idle && !combat.IsAttacking && combat.IsWaiting && waitMin == 1500 && waitMax == 1501, "후딜 뒤 Idle·BossState 대기 1500");
        Check(!combat.TryStart(0), "대기 중 시작 거부");
        combat.Tick(1500);
        Check(!combat.IsWaiting && combat.TryStart(0), "대기 종료 후 시작");
        combat.Tick(700);
        var critTarget = Target();
        Check(combat.TryHitPlayer(2001, critTarget, Script(500, 499)) == HitOutcome.Critical && calls == 2 && critTarget.Health == 52, "보스 치명 p-1·치명 피해 48");
        boss = Boss(); combat = Combat(boss); combat.TryStart(0); combat.Tick(700); var dodger = Target();
        Check(combat.TryHitPlayer(2001, dodger, Script(499)) == HitOutcome.Dodged && calls == 1 && dodger.Health == 100, "플레이어 회피: 치명 난수 0회");

        // 패턴2 휘두르기: 독립 2창, 각 창 같은 대상 1회, 창 전환 후 재등록. 피해 28.
        boss = Boss(); combat = Combat(boss); p = Target(); var other = Target();
        Check(combat.TryStart(1) && boss.CurrentState == StateKey.Attack2, "2연타 시작");
        combat.Tick(500);
        Check(combat.HitIndex == 0 && combat.IsAttackActive && combat.TryHitPlayer(2001, p, Script(9999, 9999)) == HitOutcome.Hit && p.Health == 72, "1타 피해 28");
        Check(combat.TryHitPlayer(5, other, Script(0)) == HitOutcome.Dodged, "다른 대상 1타 회피");
        combat.Tick(100); p.Tick(100);
        Check(!combat.IsAttackActive && combat.HitIndex == 1, "1타 창 종료 뒤 2타 대기");
        combat.Tick(200); p.Tick(200);
        Check(combat.IsAttackActive && combat.TryHitPlayer(2001, p, never) == HitOutcome.None && p.Health == 72, "2타 시점 피격 무적(300ms 경과)은 난수 0회 거부");
        Check(combat.TryHitPlayer(5, other, Script(9999, 9999)) == HitOutcome.Hit && other.Health == 72, "창 전환 후 같은 대상 재등록");
        combat.Tick(100); combat.Tick(400);
        Check(boss.CurrentState == StateKey.Idle && combat.IsWaiting, "2연타 종료");
        boss = Boss(); combat = Combat(boss); p = Target();
        combat.TryStart(2); combat.Tick(500); combat.TryHitPlayer(2001, p, Script(9999, 9999));
        combat.Tick(100); combat.Tick(700); p.Tick(800);
        Check(!p.IsInvulnerable && combat.TryHitPlayer(2001, p, Script(9999, 9999)) == HitOutcome.Hit && p.Health == 44, "무적 종료 뒤 2타 유효");

        // 큰 Tick과 분할 Tick: 창 시작/끝 순서·횟수·최종 상태 동일.
        List<bool> Run(Action<BossCombat> tick)
        {
            var events = new List<bool>(); var c = Combat(Boss());
            c.AttackWindowChanged += events.Add; c.TryStart(1); tick(c);
            events.Add(c.IsWaiting); events.Add(c.Boss.CurrentState == StateKey.Idle);
            return events;
        }
        var big = Run(c => c.Tick(2000));
        var split = Run(c => { for (int i = 0; i < 2000; i++) c.Tick(1); });
        Check(string.Join(",", big) == "True,False,True,False,True,True" && string.Join(",", big) == string.Join(",", split), "큰 Tick 경계 순서 보존");
        var finished = Combat(Boss()); finished.TryStart(1); finished.Tick(10000);
        Check(!finished.IsWaiting && finished.TryStart(0), "큰 Tick은 대기까지 소비");

        // 사망·노출·해체 우선: 같은 호출에서 창 정리, 이후 Tick이 상태를 덮지 않음.
        boss = Boss(); combat = Combat(boss); combat.TryStart(0); combat.Tick(700);
        boss.ReceiveHit(0, 120);
        Check(boss.IsExposed && !combat.IsAttackActive && !combat.IsAttacking && combat.TryHitPlayer(2001, Target(), never) == HitOutcome.None, "노출 즉시 창 정리·타격 거부");
        combat.Tick(5000);
        Check(boss.CurrentState == StateKey.Exposed && !combat.TryStart(0), "노출 중 Tick·시작이 상태를 덮지 않음");
        Check(boss.TryBeginExecution() && !combat.TryStart(0), "해체 중 시작 거부");
        boss.CompleteExecution(true);
        Check(boss.CurrentState == StateKey.Idle && combat.TryStart(0), "해체 종료 후 재시작");
        boss = Boss(); combat = Combat(boss); combat.TryStart(1); combat.Tick(500);
        boss.ReceiveHit(420, 0);
        combat.Tick(1000);
        Check(boss.IsDead && boss.CurrentState == StateKey.Dead && !combat.IsAttackActive && !combat.TryStart(0), "사망 우선");
        boss = Boss(); combat = Combat(boss); combat.TryStart(0); combat.Tick(700);
        boss.Tick(5000);
        Check(boss.CurrentState == StateKey.Attack1 && combat.IsAttackActive, "체세 Tick이 진행 중 패턴을 덮지 않음");
        Check(Combat(boss).TryHitPlayer(2001, Target(), never) == HitOutcome.None, "다른 전투 모듈의 창과 독립");
        var dead = Target(); dead.ReceiveHit(100);
        Check(combat.TryHitPlayer(2001, dead, never) == HitOutcome.None, "사망 대상 거부");

        // 비활성화 정리·재활성 후 재시작.
        combat.Release();
        Check(boss.CurrentState == StateKey.Idle && !combat.IsAttacking && !combat.IsWaiting && combat.TryStart(1), "Release 정리 후 재시작");
        boss = Boss(); combat = Combat(boss); boss.ReceiveHit(0, 120); combat.Release();
        Check(boss.CurrentState == StateKey.Exposed, "노출 중 Release는 상태 유지");

        // 설정 스냅샷은 호출자 배열 변경·형변환 변조에서 보호됩니다.
        var sourceHits = new[] { new AttackSpec(700, 100, 400, 0, 6, 0, 0) };
        var snapshot = new BossPattern(StateKey.Attack1, sourceHits);
        sourceHits[0] = new AttackSpec(1, 1, 0, 0, 999, 0, 0);
        Check(snapshot.Hits[0].ExtraDamage == 6 && !(snapshot.Hits is AttackSpec[]), "타격 목록 읽기 전용 스냅샷");
        Expect<NotSupportedException>(() => ((IList<AttackSpec>)snapshot.Hits)[0] = sourceHits[0]);

        // HP 비율 페이즈 전환: 현재 배포 2행(4001 HP100%·대기1500, 4002 HP50% 이하·대기1000)과 같은 구성.
        var phase0 = new BossState(4001,1006,1500,1500,2000,15,2000,"DAMAGE",1800,false,0,"Idle",10000);
        var phase1 = new BossState(4002,1006,1000,1000,2000,15,2000,"DAMAGE",1800,false,1,"Rage",5000);
        var stat = new UnitStat(1006,0,"Boss",null,420,28,4,300,500,14,120,250,0);
        BossPoise Phased(BossState start, params BossState[] rows) => new BossPoise(new Boss(stat, rows), start);
        var log = new List<string>();
        boss = Phased(phase0, phase1, phase0);
        boss.PhaseChanged += s => log.Add("phase" + s.ID);
        boss.Changed += () => log.Add("changed");
        Check(boss.Phase == phase0, "시작 페이즈 4001");
        boss.ReceiveHit(209, 0);
        Check(boss.Health == 211 && boss.Phase == phase0 && log.Count == 1, "HP 50% 초과는 전환 없음(211/420)");
        log.Clear(); boss.ReceiveHit(1, 0);
        Check(boss.Health == 210 && boss.Phase == phase1 && string.Join(",", log) == "phase4002,changed", "정확히 50%에서 전환·PhaseChanged가 Changed보다 먼저 1회");
        log.Clear(); boss.ReceiveHit(10, 0);
        Check(boss.Phase == phase1 && string.Join(",", log) == "changed", "전환 후 재통지 없음");

        boss = Phased(phase0, phase0, phase1); combat = Combat(boss); p = Target();
        boss.ReceiveHit(200, 0);
        Check(combat.TryStart(0), "페이즈1 패턴 시작");
        combat.Tick(700); boss.ReceiveHit(20, 0);
        Check(boss.Phase == phase1 && combat.IsAttacking && combat.IsAttackActive && boss.CurrentState == StateKey.Attack1, "패턴 중 전환은 진행 중 패턴을 끊지 않음");
        combat.Tick(500);
        Check(combat.IsWaiting && waitMin == 1000 && waitMax == 1001, "전환 뒤 다음 대기는 4002 대기 1000");

        boss = Phased(phase0, phase0, phase1); log.Clear(); boss.PhaseChanged += s => log.Add("phase");
        boss.ReceiveHit(420, 0);
        Check(boss.IsDead && boss.Phase == phase0 && log.Count == 0, "사망 타격은 전환하지 않음");

        boss = Phased(phase0, phase0, phase1); log.Clear(); boss.PhaseChanged += s => log.Add("phase");
        boss.ReceiveHit(160, 120);
        Check(boss.Health == 260 && boss.IsExposed && boss.Phase == phase0 && boss.TryBeginExecution(), "노출 후 해체 시작");
        Check(boss.CompleteExecution(false) && boss.Health == 185 && boss.Phase == phase1 && boss.IsExposed && log.Count == 1, "해체 피해 75로 전환·노출 유지");

        var phase2 = new BossState(4003,1006,500,500,2000,15,2000,"DAMAGE",1800,false,2,"Last",2000);
        boss = Phased(phase0, phase2, phase0, phase1); log.Clear(); boss.PhaseChanged += s => log.Add("phase" + s.ID);
        boss.ReceiveHit(370, 0);
        Check(boss.Phase == phase2 && string.Join(",", log) == "phase4003", "여러 기준을 한 번에 넘으면 마지막 행으로 1회 전환(행 순서 무관)");
        boss = Phased(phase1, phase0, phase1); boss.ReceiveHit(400, 0);
        Check(boss.Phase == phase1, "뒤 페이즈로 시작하면 이전 행으로 돌아가지 않음");
        Expect<ArgumentException>(() => Phased(phase0, phase0, new BossState(4002,1006,1000,1000,2000,15,2000,"DAMAGE",1800,false,1,"Rage",10000)));
        Expect<ArgumentException>(() => Phased(phase0, phase0, phase1, new BossState(4003,1006,1000,1000,2000,15,2000,"DAMAGE",1800,false,1,"Rage",3000)));
        Expect<ArgumentException>(() => Phased(phase0, phase0, new BossState(4002,1006,1000,1000,2000,15,2000,"DAMAGE",1800,false,1,"Rage",0)));
        Expect<ArgumentException>(() => Phased(phase0, phase0, phase1, new BossState(4003,1006,500,500,2000,15,2000,"DAMAGE",1800,false,2,"Last",6000)));

        // 페이즈2 문 돌진(C09): 예비 1000·추가 +14·벽 체세 30은 기획 값, 돌진 상한 2000·후딜 400·속도 900은 검사 전용.
        BossPattern[] ChargePatterns() => new[]
        {
            new BossPattern(StateKey.Attack1, new[] { new AttackSpec(700, 100, 400, 0, 6, 0, 0) }),
            new BossPattern(StateKey.Skill1, new[] { new AttackSpec(1000, 2000, 400, 0, 14, 0, 0) }, 1, 900, 30)
        };
        BossCombat ChargeCombat(BossPoise b) => new BossCombat(b, ChargePatterns(), (min, max) => { waitMin = min; waitMax = max; return min; });
        boss = Phased(phase0, phase0, phase1); combat = ChargeCombat(boss); p = Target();
        Check(!combat.TryStart(1) && !combat.IsAttacking, "페이즈1에서 문 돌진 거부");
        boss.ReceiveHit(210, 0);
        combat.Face(-1);
        Check(combat.TryStart(1) && boss.CurrentState == StateKey.Skill1 && combat.Velocity == 0 && combat.Facing == -1, "페이즈2 문 돌진 시작·예비 동작 중 정지");
        combat.Face(1);
        Check(combat.Facing == -1 && !combat.NotifyWallHit(), "패턴 중 방향 고정·예비 동작 중 벽 무시");
        combat.Tick(1000);
        Check(combat.IsCharging && combat.Velocity == -900, "예비 1000 뒤 돌진 속도");
        Check(combat.TryHitPlayer(2001, p, false, false) && p.Health == 58, "돌진 피해 28+14=42");
        Check(combat.NotifyWallHit() && !combat.IsAttackActive && combat.Velocity == 0 && boss.Poise == 90 && combat.IsAttacking, "벽 충돌: 돌진 종료·체세 −30·후딜");
        combat.Tick(400);
        Check(boss.CurrentState == StateKey.Idle && waitMin == 1000, "벽 충돌 후 후딜 400 뒤 대기 1000");
        combat.Tick(1000);
        Check(combat.TryStart(1), "돌진 재시작");
        combat.Tick(3000);
        Check(!combat.IsCharging && combat.IsAttacking && boss.Poise == 90, "벽 없이 상한 2000이 지나면 체세 피해 없이 후딜");
        combat.Tick(400); combat.Tick(1000);
        boss.ReceiveHit(0, 60);
        Check(combat.TryStart(1), "체세 30 남은 상태 돌진");
        combat.Tick(1000);
        Check(combat.NotifyWallHit() && boss.IsExposed && !combat.IsAttacking && combat.Velocity == 0, "벽 충돌로 체세 0이면 노출이 패턴 정리");
        // 창 열림 콜백 안 벽 충돌(동기 접촉): 배포 체세 80, 후딜 0/400. 끝난 패턴을 진행하지 않고 체세는 한 번만 깎습니다.
        var stat80 = new UnitStat(1006,0,"Boss",null,420,28,4,300,500,14,80,250,0);
        foreach (int recovery in new[] { 0, 400 })
        foreach (int poiseHit in new[] { 60, 0 })
        {
            var b = new BossPoise(new Boss(stat80, new[] { phase0, phase1 }), phase0);
            b.ReceiveHit(210, poiseHit);
            var c = new BossCombat(b, new[] { new BossPattern(StateKey.Skill1, new[] { new AttackSpec(1000, 2000, recovery, 0, 14, 0, 0) }, 1, 900, 30) },
                (min, max) => { waitMin = min; return min; });
            c.AttackWindowChanged += active => { if (active) c.NotifyWallHit(); };
            waitMin = -1;
            Check(c.TryStart(0), "동기 벽 충돌 돌진 시작");
            c.Tick(1000);
            bool exposed = poiseHit == 60;
            Check(b.Health == 210 && b.Poise == (exposed ? 0 : 50) && b.IsExposed == exposed && !c.IsAttackActive && c.Velocity == 0 &&
                c.IsAttacking == (!exposed && recovery > 0) && (exposed || recovery > 0 || (b.CurrentState == StateKey.Idle && waitMin == 1000)),
                "동기 벽 충돌 후딜" + recovery + " 노출" + exposed);
            c.Tick(recovery + 1000);
            Check(b.IsExposed == exposed && b.Poise == (exposed ? 0 : 50) && !c.IsAttacking, "동기 벽 충돌 이후 Tick 안전·중복 체세 감소 없음");
        }
        Expect<ArgumentOutOfRangeException>(() => new BossPattern(StateKey.Skill1, new[] { new AttackSpec(1, 1, 0, 0, 0, 0, 0), new AttackSpec(1, 1, 0, 0, 0, 0, 0) }, 1, 900, 30));
        Expect<ArgumentOutOfRangeException>(() => new BossPattern(StateKey.Attack1, new[] { new AttackSpec(1, 1, 0, 0, 0, 0, 0) }, 0, 900, 0));
        Expect<ArgumentOutOfRangeException>(() => new BossPattern(StateKey.Attack1, new[] { new AttackSpec(1, 1, 0, 0, 0, 0, 0) }, 0, 0, 30));
        Expect<ArgumentOutOfRangeException>(() => new BossPattern(StateKey.Attack1, new[] { new AttackSpec(1, 1, 0, 0, 0, 0, 0) }, -1));
        Expect<ArgumentOutOfRangeException>(() => combat.Face(0));

        // 입력 경계
        Expect<ArgumentOutOfRangeException>(() => new BossPattern(StateKey.Skill1, new[] { new AttackSpec(1, 1, 0, 0, 0, 0, 0) }));
        Expect<ArgumentException>(() => new BossPattern(StateKey.Attack1, new AttackSpec[0]));
        Expect<ArgumentException>(() => new BossCombat(Boss(), new BossPattern[0], (min, max) => min));
        Expect<ArgumentNullException>(() => new BossCombat(Boss(), Patterns(), null));
        Expect<ArgumentOutOfRangeException>(() => Combat(Boss()).TryStart(3));
        Expect<ArgumentOutOfRangeException>(() => Combat(Boss()).Tick(-1));
        Expect<ArgumentNullException>(() => Combat(Boss()).TryHitPlayer(2001, Target(), (Func<int, int, int>)null));
        Expect<InvalidOperationException>(() => { var c = new BossCombat(Boss(), Patterns(), (min, max) => max); c.TryStart(0); c.Tick(1200); });
        return checks;
    }
}
