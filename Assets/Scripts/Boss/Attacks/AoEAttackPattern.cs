using System.Collections.Generic;
using Core.Boss.AoE;
using Core.Boss.Projectiles;
using Core.Combat;
using UnityEngine;

namespace Core.Boss.Attacks
{
    /// <summary>
    /// Pattern 4: AoE 공격 패턴.
    /// 공중 연출 중 fire prefab을 떨어뜨리고, 착지 시점과 장판 발동 시점을 동기화한다.
    /// </summary>
    public class AoEAttackPattern : IBossAttackPattern
    {
        private enum PatternPhase
        {
            Complete,
            TakeOff,
            FlyForward,
            Casting,
            Landing
        }

        private readonly BossController.AoEAttackSettings _settings;
        private readonly List<AoECircleController> _circlePool = new List<AoECircleController>(8);
        private readonly List<AoECircleController> _activeCircles = new List<AoECircleController>(8);

        private PatternPhase _phase = PatternPhase.Complete;
        private float _phaseTimer;
        private float _spawnTimer;
        private int _spawnedCount;
        private float _impactSyncTime;
        private Vector3 _lastTargetPosition;
        private bool _hasLastTargetPosition;

        public AoEAttackPattern(BossController.AoEAttackSettings settings)
        {
            _settings = settings;
        }

        public void Enter(BossController controller)
        {
            if (_settings == null || _settings.circlePrefab == null)
            {
                controller.SetLocomotionVisualSuppressed(false);
                Debug.LogWarning("AoEAttackPattern: circlePrefab is not assigned.");
                _phase = PatternPhase.Complete;
                return;
            }

            CleanupActiveCircles();
            EnsureCirclePool(Mathf.Max(1, _settings.circleCount));

            // 공중 연출 시작 시 지상 Locomotion 애니메이션 진입을 잠금한다.
            controller.SetLocomotionVisualSuppressed(true);
            controller.StopMoving();
            if (controller.Target != null)
            {
                controller.RotateTowards(controller.Target.position);
            }
            UpdateTargetPositionHistory(controller);

            _impactSyncTime = _settings.impactSyncTime > 0f
                ? _settings.impactSyncTime
                : _settings.telegraphDuration;
            _impactSyncTime = Mathf.Max(0.05f, _impactSyncTime);

            _spawnedCount = 0;
            _spawnTimer = 0f;
            _phaseTimer = 0f;
            _phase = PatternPhase.TakeOff;

            controller.Visual?.PlayTakeOff();
        }

        public bool Update(BossController controller)
        {
            if (_phase == PatternPhase.Complete) return true;

            switch (_phase)
            {
                case PatternPhase.TakeOff:
                    UpdateTakeOff(controller);
                    break;
                case PatternPhase.FlyForward:
                    UpdateFlyForward(controller);
                    break;
                case PatternPhase.Casting:
                    UpdateCasting(controller);
                    break;
                case PatternPhase.Landing:
                    UpdateLanding(controller);
                    break;
            }

            PruneInactiveCircles();
            UpdateTargetPositionHistory(controller);
            return _phase == PatternPhase.Complete;
        }

        public void Exit(BossController controller)
        {
            controller.SetLocomotionVisualSuppressed(false);
            CleanupActiveCircles();
            _phase = PatternPhase.Complete;
            _phaseTimer = 0f;
            _spawnTimer = 0f;
            _spawnedCount = 0;
            _hasLastTargetPosition = false;
        }

        private void UpdateTakeOff(BossController controller)
        {
            _phaseTimer += Time.deltaTime;
            if (_phaseTimer < _settings.takeOffDuration) return;

            _phase = PatternPhase.FlyForward;
            _phaseTimer = 0f;
            controller.Visual?.PlayFlyForward();
        }

        private void UpdateFlyForward(BossController controller)
        {
            _phaseTimer += Time.deltaTime;

            bool enteredCasting = false;
            if (controller.Target != null)
            {
                Vector3 toTarget = controller.Target.position - controller.transform.position;
                toTarget.y = 0f;

                float distance = toTarget.magnitude;
                if (distance > _settings.castRange && toTarget.sqrMagnitude > 0.0001f)
                {
                    controller.RotateTowards(controller.Target.position);
                    controller.MoveRaw(toTarget.normalized, _settings.flyForwardSpeed);
                }
                else
                {
                    EnterCastingPhase(controller);
                    enteredCasting = true;
                }
            }

            // 타겟이 없거나 제한 시간 초과 시에도 캐스팅으로 진입
            if (!enteredCasting && _phaseTimer >= _settings.flyForwardDuration)
            {
                EnterCastingPhase(controller);
            }
        }

        private void UpdateCasting(BossController controller)
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnedCount < _settings.circleCount && _spawnTimer <= 0f)
            {
                SpawnAoEInstance(controller);
                _spawnedCount++;
                _spawnTimer = Mathf.Max(0f, _settings.spawnInterval);
            }

            if (_spawnedCount < _settings.circleCount) return;
            if (_activeCircles.Count > 0) return;

            _phase = PatternPhase.Landing;
            _phaseTimer = 0f;
            controller.Visual?.PlayLand();
        }

        private void UpdateLanding(BossController controller)
        {
            _phaseTimer += Time.deltaTime;
            if (_phaseTimer >= _settings.landDuration)
            {
                _phase = PatternPhase.Complete;
            }
        }

        private void SpawnAoEInstance(BossController controller)
        {
            Vector3 impactPoint = ResolveImpactPoint(controller);
            AoECircleController circle = AcquireCircle();
            if (circle == null) return;

            circle.StartTelegraph(
                impactPoint,
                _settings.radius,
                _impactSyncTime,
                _settings.activeDuration,
                _settings.tickInterval,
                _settings.damage,
                controller.gameObject.GetInstanceID(),
                _settings.targetMask,
                BossAttackHitType.Attack4Projectile);
            _activeCircles.Add(circle);

            SpawnImpactProjectile(controller, impactPoint);
        }

        private void EnterCastingPhase(BossController controller)
        {
            _phase = PatternPhase.Casting;
            _phaseTimer = 0f;
            _spawnTimer = 0f;
            controller.Visual?.PlayFlyIdle();
        }

        private void SpawnImpactProjectile(BossController controller, Vector3 impactPoint)
        {
            BossProjectilePool pool = controller.ProjectilePool;
            if (pool == null)
            {
                return;
            }

            BossProjectile projectile = pool.TryGetProjectile();
            if (projectile == null)
            {
                return;
            }

            Vector3 startPos;
            if (controller.ProjectileSpawnPoint != null)
            {
                startPos = controller.ProjectileSpawnPoint.position;
                if (startPos.y <= impactPoint.y + 0.05f)
                {
                    startPos.y = impactPoint.y + Mathf.Max(0.5f, _settings.fallbackProjectileHeight);
                }
            }
            else
            {
                startPos = impactPoint + Vector3.up * Mathf.Max(0.5f, _settings.fallbackProjectileHeight);
            }

            projectile.gameObject.SetActive(true);
            projectile.InitializeImpactMarker(
                startPos,
                impactPoint,
                _impactSyncTime,
                controller.gameObject.GetInstanceID());
        }

        private Vector3 ResolveImpactPoint(BossController controller)
        {
            Vector3 center = controller.Target != null
                ? controller.Target.position
                : controller.transform.position + controller.transform.forward * controller.AoEAttackRange;

            Vector3 heading = ResolveTargetHeading(controller, out float targetSpeed);
            float baseSpread = Mathf.Max(0f, _settings.spawnSpreadRadius);
            float forwardSpread = _settings.forwardSpreadRadius > 0f ? _settings.forwardSpreadRadius : baseSpread;
            float sideSpread = _settings.sideSpreadRadius > 0f ? _settings.sideSpreadRadius : baseSpread;
            float headingBias = Mathf.Clamp01(_settings.headingBias);
            float leadTime = Mathf.Max(0f, _settings.headingLeadTime);
            float maxLeadDistance = Mathf.Max(0f, _settings.maxHeadingLeadDistance);
            float leadDistance = Mathf.Min(maxLeadDistance, targetSpeed * leadTime);
            Vector3 predictedCenter = center + heading * leadDistance;

            bool useHeadingSpread =
                heading.sqrMagnitude > 0.0001f &&
                targetSpeed >= Mathf.Max(0f, _settings.headingMinSpeed);

            Vector3 candidate;
            if (useHeadingSpread && forwardSpread > 0.001f && sideSpread > 0.001f)
            {
                Vector3 right = new Vector3(-heading.z, 0f, heading.x);
                float angle = Random.value * Mathf.PI * 2f;
                float radius01 = Mathf.Sqrt(Random.value);
                float lateral = Mathf.Cos(angle) * radius01;
                float forward = Mathf.Sin(angle) * radius01;
                float biasedForward = Mathf.Lerp(forward, Mathf.Abs(forward), headingBias);
                candidate = predictedCenter + (right * (lateral * sideSpread)) + (heading * (biasedForward * forwardSpread));
            }
            else
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(0f, baseSpread);
                candidate = center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            }

            Vector3 rayOrigin = candidate + Vector3.up * Mathf.Max(0.5f, _settings.groundRayHeight);
            bool hitGround = false;
            float rayDistance = Mathf.Max(0.5f, _settings.groundRayDistance);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, _settings.groundMask, QueryTriggerInteraction.Ignore))
            {
                candidate = hit.point;
                hitGround = true;
            }
            else
            {
                // First fallback: common ground layers to survive inspector mask mismatch.
                int commonGroundMask = LayerMask.GetMask("Ground", "Default");
                if (commonGroundMask != 0 &&
                    Physics.Raycast(rayOrigin, Vector3.down, out hit, rayDistance, commonGroundMask, QueryTriggerInteraction.Ignore))
                {
                    candidate = hit.point;
                    hitGround = true;
                }
                else
                {
                    // Second fallback: all non-target layers (exclude player/targets).
                    int nonTargetMask = ~_settings.targetMask.value;
                    if (Physics.Raycast(rayOrigin, Vector3.down, out hit, rayDistance, nonTargetMask, QueryTriggerInteraction.Ignore))
                    {
                        candidate = hit.point;
                        hitGround = true;
                    }
                }
            }

            if (!hitGround)
            {
                // Last resort: keep it near boss base instead of target body height.
                candidate.y = controller.transform.position.y;
            }

            candidate.y += _settings.groundOffset;
            return candidate;
        }

        private Vector3 ResolveTargetHeading(BossController controller, out float planarSpeed)
        {
            planarSpeed = 0f;

            Transform target = controller.Target;
            if (target == null)
            {
                return FlattenOrFallback(controller.transform.forward);
            }

            CharacterController targetCharacterController = target.GetComponent<CharacterController>();
            if (targetCharacterController != null)
            {
                Vector3 velocity = targetCharacterController.velocity;
                velocity.y = 0f;
                planarSpeed = velocity.magnitude;
                if (planarSpeed >= Mathf.Max(0f, _settings.headingMinSpeed) && velocity.sqrMagnitude > 0.0001f)
                {
                    return velocity / planarSpeed;
                }
            }

            Rigidbody targetRigidbody = target.GetComponent<Rigidbody>();
            if (targetRigidbody != null)
            {
                // Unity 2022 호환: Rigidbody.linearVelocity 대신 velocity를 사용한다.
                Vector3 velocity = targetRigidbody.velocity;
                velocity.y = 0f;
                planarSpeed = velocity.magnitude;
                if (planarSpeed >= Mathf.Max(0f, _settings.headingMinSpeed) && velocity.sqrMagnitude > 0.0001f)
                {
                    return velocity / planarSpeed;
                }
            }

            if (_hasLastTargetPosition)
            {
                Vector3 delta = target.position - _lastTargetPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                    planarSpeed = delta.magnitude / dt;
                    if (planarSpeed >= Mathf.Max(0f, _settings.headingMinSpeed))
                    {
                        return delta.normalized;
                    }
                }
            }

            Vector3 targetForward = target.forward;
            targetForward.y = 0f;
            if (targetForward.sqrMagnitude > 0.0001f)
            {
                return targetForward.normalized;
            }

            Vector3 toTarget = target.position - controller.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                return toTarget.normalized;
            }

            return FlattenOrFallback(controller.transform.forward);
        }

        private void UpdateTargetPositionHistory(BossController controller)
        {
            Transform target = controller.Target;
            if (target == null)
            {
                _hasLastTargetPosition = false;
                return;
            }

            _lastTargetPosition = target.position;
            _hasLastTargetPosition = true;
        }

        private static Vector3 FlattenOrFallback(Vector3 vector)
        {
            vector.y = 0f;
            if (vector.sqrMagnitude > 0.0001f)
            {
                return vector.normalized;
            }

            return Vector3.forward;
        }

        private AoECircleController AcquireCircle()
        {
            for (int i = 0; i < _circlePool.Count; i++)
            {
                AoECircleController circle = _circlePool[i];
                if (circle == null) continue;
                if (circle.IsRunning) continue;
                if (circle.gameObject.activeSelf) circle.gameObject.SetActive(false);
                return circle;
            }

            if (_circlePool.Count >= Mathf.Max(1, _settings.maxCircleInstances))
            {
                Debug.LogWarning("AoEAttackPattern: circle pool exhausted.");
                return null;
            }

            AoECircleController instance = _settings.circleRoot != null
                ? Object.Instantiate(_settings.circlePrefab, _settings.circleRoot)
                : Object.Instantiate(_settings.circlePrefab);
            instance.gameObject.SetActive(false);
            _circlePool.Add(instance);
            return instance;
        }

        private void EnsureCirclePool(int neededCount)
        {
            int targetCount = Mathf.Min(Mathf.Max(1, _settings.maxCircleInstances), neededCount);
            while (_circlePool.Count < targetCount)
            {
                AoECircleController instance = _settings.circleRoot != null
                    ? Object.Instantiate(_settings.circlePrefab, _settings.circleRoot)
                    : Object.Instantiate(_settings.circlePrefab);
                instance.gameObject.SetActive(false);
                _circlePool.Add(instance);
            }
        }

        private void PruneInactiveCircles()
        {
            for (int i = _activeCircles.Count - 1; i >= 0; i--)
            {
                AoECircleController circle = _activeCircles[i];
                if (circle == null || !circle.IsRunning)
                {
                    _activeCircles.RemoveAt(i);
                }
            }
        }

        private void CleanupActiveCircles()
        {
            for (int i = 0; i < _activeCircles.Count; i++)
            {
                if (_activeCircles[i] != null)
                {
                    _activeCircles[i].ForceEnd();
                }
            }
            _activeCircles.Clear();
        }
    }
}
