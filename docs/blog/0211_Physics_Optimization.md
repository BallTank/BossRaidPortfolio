# ⚡ 화려한 드래곤 뒤에 숨겨진 최적화 기술: Physics & Collider

> **"드래곤 모델은 3만 폴리곤인데, 이걸 통째로 Mesh Collider로 감싸면 게임이 어떻게 될까?"**

보스 캐릭터의 외형이 복잡해질수록, 정확한 피격 판정과 성능 사이의 딜레마는 커진다.
오늘은 큐브(Cube)에서 드래곤(Dragon)으로 에셋을 교체하면서 적용한 **가비지 컬렉션(GC) 방지**와 **물리 연산 최적화** 기법을 소개한다.

---

## 1. Mesh Collider의 함정

처음 드래곤 에셋을 적용했을 때, 가장 쉬운 방법은 `Mesh Collider`를 컴포넌트에 추가하는 것이었다. 하지만 이는 치명적인 단점이 있다.

1.  **비싼 연산 비용**: 3만 개의 삼각형(Triangle)과 매 프레임 충돌 검사를 하는 것은 CPU 낭비다.
2.  **부정확한 판정**: 애니메이션으로 몸이 휘어질 때, Mesh Collider가 실시간으로 변형(Deform)되게 하려면 `Skinned Mesh Renderer`와 연동해야 하는데, 이는 엄청난 부하를 준다.

---

## 2. 해결책 A: Compound Collider (복합 충돌체)

우리는 무거운 Mesh Collider 대신, **여러 개의 단순한 Primitive Collider**를 뼈(Bone)에 심는 방식을 택했다.

![Compound Collider Structure](https://docs.unity3d.com/uploads/Main/CompoundCollider.png)
*(실제 구현 구조 예시)*

*   **Head Bone** -> `Sphere Collider` (약점 판정)
*   **Spine Bones** -> `Box Collider` 여러 개
*   **Tail Bones** -> `Capsule Collider` 연쇄

### `BossHitBox.cs`의 역할
각 Collider에는 `BossHitBox`라는 가벼운 스크립트를 붙여, 피격 시 본체(`BossHealth`)로 데미지를 전달하도록 했다.

```csharp
public class BossHitBox : MonoBehaviour, IDamageable
{
    [SerializeField] private BossHealth _health;
    [SerializeField] private float _damageMultiplier = 1.0f; // 머리는 2.0배!

    public void TakeDamage(int damage)
    {
        // 뼈에 맞았지만, 데미지는 본체가 입는다.
        _health.TakeDamage((int)(damage * _damageMultiplier));
    }
}
```

이 방식의 장점은 **연산 비용이 매우 저렴**(`Sphere` vs `Sphere` 충돌은 단순 거리 계산)하면서도, 애니메이션에 따라 **판정 범위가 정확하게 따라움직인다**는 것이다.

---

## 3. 해결책 B: NonAlloc API (Zero-GC)

플레이어나 보스가 공격할 때, 보통 `Physics.OverlapSphere`를 사용해 주변 적을 찾는다.
하지만 순정 `OverlapSphere`는 호출할 때마다 `Collider[]` 배열을 새로 생성(new)하여 힙 메모리를 할당한다. 이는 **GC Spike(프레임 드랍)**의 주범이다.

### Bad Code: 매 프레임 메모리 할당
```csharp
void Update()
{
    // ❌ 매번 배열을 새로 만듦 -> GC 발생
    Collider[] hits = Physics.OverlapSphere(transform.position, radius);
}
```

### Good Code: NonAlloc + Pre-allocation
우리는 `Physics.OverlapSphereNonAlloc`을 도입하여 메모리 할당을 **0(Zero)**으로 만들었다.

```csharp
public class DamageCaster : MonoBehaviour
{
    // ✅ 1. 배열을 미리 만들어두고 재사용 (Object Pool 개념)
    private Collider[] _hitResults = new Collider[10]; 

    public void EnableHitbox(int damage)
    {
        // ✅ 2. NonAlloc API 사용: 결과만 배열에 채워 넣음
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position, 
            _radius, 
            _hitResults, 
            _targetLayer
        );

        for (int i = 0; i < hitCount; i++)
        {
            var target = _hitResults[i].GetComponent<IDamageable>();
            target?.TakeDamage(damage);
        }
    }
}
```

---

## 4. 결론

화려한 그래픽 뒤에는 항상 차가운 최적화 로직이 숨어있어야 한다.

*   **복합적인 외형**은 `Compound Collider`로 단순화하여 물리 엔진을 속이자.
*   **잦은 물리 연산**은 `NonAlloc` API로 메모리를 아끼자.

이 두 가지만 지켜도, 수십 마리의 드래곤이 나오는 씬에서도 쾌적한 프레임을 유지할 수 있다.

---
*End of Series: Boss Pattern Strategy & Optimization*
