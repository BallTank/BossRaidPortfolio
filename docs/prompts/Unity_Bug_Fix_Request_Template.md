# Unity Bug Fix Request Template

## Project / Scene
- Project / Scene:
- Date:

## 1) Goal
- What I want:
- Why this matters:

## 2) Problem
- Actual problem:
- Expected result:

Steps to make the bug happen:
1.
2.
3.

- When it happens:
- When it does not happen:

## 3) Related Context
- Related scripts:
- Related systems:
Examples: input, state machine, animation, combat, camera, UI

- Console log / error:
- Things that must not break:
- Pass condition (what result means "fixed"):

## 4) Diagnosis Request (No code yet)
Please use only this context first.
If context is missing, tell me exactly what is missing.
Do not write code yet.

Please give me:
1. Top 3 root-cause guesses
2. Why each guess fits
3. One quick way to prove each guess wrong
4. The smallest debug log or Unity test for each guess
5. Which guess I should check first
6. The logic flow in order
7. Mark each step as clean or suspicious
8. Two explanations:
   - Detailed version for coding
   - Easy version for me

## 5) Code Fix Request
Now write the fix.

Rules:
- Fix the root cause, not only the symptom
- Keep the change as small as possible
- Do not break existing behavior
- Reuse the current system if possible
- Add only the minimum debug logs needed

Also tell me:
- Which file and function will change
- What I should test first in Unity
- What risk still remains
- Easy explanation of what changed

## 6) After Testing
I tested it.

- What worked:
- What failed:

Steps I tested now:
1.
2.
3.

- Console logs:
- New strange behavior:

Please tell me:
1. What is still wrong
2. What the logs mean
3. Which root-cause guess is strongest now
4. The next smallest fix
5. Whether temporary logs should stay or be removed
6. Easy explanation

