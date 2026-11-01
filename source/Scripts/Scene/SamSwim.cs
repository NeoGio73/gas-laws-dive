using UnityEngine;

namespace DiveGame.Scene
{
    // Sam's swimming pose on his humanoid rig, set after any animation each frame: arms relaxed along
    // his sides, head lifted to look ahead, and (unless a swim clip moves the legs) a slow flutter
    // kick from the hips with the knees following and the toes pointed, so the fins trail behind.
    // The same in both versions. Directions are taken in the model's own standing frame (up = head,
    // forward = face, right = his left-to-right), so they hold however the model is turned.
    [RequireComponent(typeof(Animator))]
    public class SamSwim : MonoBehaviour
    {
        public bool clipMovesLegs;            // true once a swim clip (Mixamo) drives the legs
        public float kickDegrees = 13f;       // each leg swings this far either side
        public float kicksPerSecond = 0.55f;  // slow, as a relaxed diver kicks
        public float kneeDegrees = 16f;
        public float headLift = 42f;

        Animator anim;
        Transform upperArmL, lowerArmL, handL, upperArmR, lowerArmR, handR;
        Transform upperLegL, lowerLegL, footL, upperLegR, lowerLegR, footR, neck, head;
        Quaternion[] rest;
        Transform[] bones;
        Vector3 toeL, toeR;   // each foot's toe direction, in the foot's own space
        // Every other bone's rest pose. A humanoid clip also places the body (the swimmer in the clip
        // lies flat, so it moves Sam's hips about a metre toward his feet) even through a legs-only
        // mask; only the leg bones are taken from the clip, everything else is put back.
        Transform[] others;
        Vector3[] otherPos;
        Quaternion[] otherRot;

        void Start()
        {
            anim = GetComponent<Animator>();
            Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
            upperArmL = B(HumanBodyBones.LeftUpperArm); lowerArmL = B(HumanBodyBones.LeftLowerArm); handL = B(HumanBodyBones.LeftHand);
            upperArmR = B(HumanBodyBones.RightUpperArm); lowerArmR = B(HumanBodyBones.RightLowerArm); handR = B(HumanBodyBones.RightHand);
            upperLegL = B(HumanBodyBones.LeftUpperLeg); lowerLegL = B(HumanBodyBones.LeftLowerLeg); footL = B(HumanBodyBones.LeftFoot);
            upperLegR = B(HumanBodyBones.RightUpperLeg); lowerLegR = B(HumanBodyBones.RightLowerLeg); footR = B(HumanBodyBones.RightFoot);
            neck = B(HumanBodyBones.Neck); head = B(HumanBodyBones.Head);
            bones = new[] { upperArmL, lowerArmL, handL, upperArmR, lowerArmR, handR, upperLegL, lowerLegL, footL, upperLegR, lowerLegR, footR, neck, head };
            rest = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) if (bones[i]) rest[i] = bones[i].localRotation;
            if (footL) toeL = footL.InverseTransformDirection(transform.forward);   // in the T-pose the toes point forward
            if (footR) toeR = footR.InverseTransformDirection(transform.forward);

            var legs = new System.Collections.Generic.HashSet<Transform> { upperLegL, lowerLegL, footL, upperLegR, lowerLegR, footR,
                B(HumanBodyBones.LeftToes), B(HumanBodyBones.RightToes) };
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && !legs.Contains(t)) list.Add(t);
            others = list.ToArray();
            otherPos = new Vector3[others.Length]; otherRot = new Quaternion[others.Length];
            for (int i = 0; i < others.Length; i++) { otherPos[i] = others[i].localPosition; otherRot[i] = others[i].localRotation; }
        }

        void LateUpdate()
        {
            if (bones == null) return;
            if (clipMovesLegs)
                for (int i = 0; i < others.Length; i++) others[i].SetLocalPositionAndRotation(otherPos[i], otherRot[i]);
            // Without a clip, start each frame from the rest pose; with one, from the clip's pose.
            for (int i = 0; i < bones.Length; i++)
            {
                if (!bones[i]) continue;
                bool leg = i >= 6 && i <= 11;
                if (!(clipMovesLegs && leg)) bones[i].localRotation = rest[i];
            }
            Vector3 up = transform.up, fwd = transform.forward, right = transform.right;

            // Arms: relaxed along his sides, a little out and a little under him, elbows soft.
            Arm(upperArmL, lowerArmL, handL, -right, up, fwd);
            Arm(upperArmR, lowerArmR, handR, right, up, fwd);

            // Head: lifted so the mask looks ahead, shared between neck and head.
            if (neck) neck.rotation = Quaternion.AngleAxis(-headLift * 0.55f, right) * neck.rotation;
            if (head) head.rotation = Quaternion.AngleAxis(-headLift * 0.45f, right) * head.rotation;

            if (clipMovesLegs)
            {
                // the clip bends the hips and knees; the toes are pointed here, so the fins trail
                PointToes(upperLegL, footL, toeL, right);
                PointToes(upperLegR, footR, toeR, right);
                return;
            }
            float t = Time.timeSinceLevelLoad * kicksPerSecond * Mathf.PI * 2f;
            Leg(upperLegL, lowerLegL, footL, toeL, Mathf.Sin(t), Mathf.Sin(t - 0.9f), up, right);
            Leg(upperLegR, lowerLegR, footR, toeR, Mathf.Sin(t + Mathf.PI), Mathf.Sin(t + Mathf.PI - 0.9f), up, right);
        }

        // The fin carries on along the whole leg (hip to ankle), not the bent shin, so it trails
        // nearly in line with his body as a diver's fins do.
        static void PointToes(Transform thigh, Transform foot, Vector3 toeLocal, Vector3 right)
        {
            if (!thigh || !foot) return;
            Vector3 legDir = foot.position - thigh.position;
            if (legDir.sqrMagnitude < 1e-8f) return;
            Vector3 cur = foot.TransformDirection(toeLocal);
            Vector3 want = legDir.normalized;
            foot.rotation = Quaternion.FromToRotation(cur, want) * foot.rotation;
        }

        static void Aim(Transform bone, Transform child, Vector3 dir)
        {
            if (!bone || !child) return;
            Vector3 cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(cur.normalized, dir.normalized) * bone.rotation;
        }

        static void Arm(Transform upper, Transform lower, Transform hand, Vector3 side, Vector3 up, Vector3 fwd)
        {
            Vector3 upperDir = -up * 0.94f + side * 0.2f + fwd * 0.16f;
            Aim(upper, lower, upperDir);
            Aim(lower, hand, Quaternion.AngleAxis(0f, side) * (-up * 0.9f + side * 0.08f + fwd * 0.32f));
        }

        void Leg(Transform thigh, Transform shin, Transform foot, Vector3 toeLocal, float kick, float knee, Vector3 up, Vector3 right)
        {
            if (!thigh || !shin) return;
            Vector3 thighDir = Quaternion.AngleAxis(kick * kickDegrees, right) * -up;
            Aim(thigh, shin, thighDir);
            // the knee bends only one way: most on the down-kick, almost straight on the up-kick
            float bend = kneeDegrees * (0.35f + 0.65f * Mathf.Clamp01(0.5f + 0.5f * knee));
            Vector3 shinDir = Quaternion.AngleAxis(bend, right) * thighDir;
            Aim(shin, foot, shinDir);
            if (!foot) return;
            // toes pointed: the foot (and its fin) carries on along the shin
            Vector3 cur = foot.TransformDirection(toeLocal);
            Vector3 want = Quaternion.AngleAxis(bend * 0.4f, right) * shinDir;
            foot.rotation = Quaternion.FromToRotation(cur, want) * foot.rotation;
        }
    }
}
