using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GrannyWorld.EditorTools
{
    /// <summary>
    /// Generates procedural Humanoid animation clips (idle, walk, run, jump, fall, flip-tuck, land)
    /// by writing Mecanim muscle curves, then wires them into an Animator Controller.
    /// Humanoid clips retarget onto any humanoid, so Mixamo clips can replace these later.
    ///
    /// Muscle conventions measured on this avatar:
    ///   Upper Leg Front-Back: 0.6 = straight down, lower = forward, 1 = ~20° back
    ///   Lower Leg Stretch:    1 = straight, -1 = fully bent
    ///   Arm Down-Up:          0.4 = horizontal, -0.55 = relaxed at sides, 1 = up
    ///   Arm Front-Back:       0.3 = neutral, higher = forward
    ///   Forearm Stretch:      1 = straight, 0 = ~80° bent
    ///   Spine/Chest Front-Back: + = bend forward;  Head/Neck Nod: + = look up
    /// </summary>
    public static class GrannyAnimationBuilder
    {
        const string Folder = "Assets/GrannyWorld/Animations";
        const float FrameRate = 30f;

        delegate void Pose(float t, float[] m, ref float rootY);

        [MenuItem("Tools/Granny World/Generate Animations")]
        public static string Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/GrannyWorld", "Animations");

            var idle = MakeClip("Granny_Idle", 3.2f, true, IdlePose);
            var walk = MakeClip("Granny_Walk", 0.9f, true, WalkPose);
            var run = MakeClip("Granny_Run", 0.56f, true, RunPose);
            var jump = MakeClip("Granny_JumpUp", 0.4f, false, JumpPose);
            var fall = MakeClip("Granny_Fall", 0.9f, true, FallPose);
            var tuck = MakeClip("Granny_Tuck", 0.5f, true, TuckPose);
            var land = MakeClip("Granny_Land", 0.4f, false, LandPose);

            BuildController(idle, walk, run, jump, fall, tuck, land);
            AssetDatabase.SaveAssets();
            return "Generated 7 clips and Granny.controller";
        }

        // ---------------------------------------------------------------- poses
        static void IdlePose(float t, float[] m, ref float y)
        {
            float ph = t * Mathf.PI * 2f;
            float breath = Mathf.Sin(ph * 2f);
            Add(m, "Chest Front-Back", -0.04f * breath);
            Add(m, "Spine Front-Back", 0.03f);
            Add(m, "Spine Left-Right", 0.04f * Mathf.Sin(ph));
            Add(m, "Left Shoulder Down-Up", 0.05f * breath);
            Add(m, "Right Shoulder Down-Up", 0.05f * breath);
            Add(m, "Left Arm Down-Up", 0.03f * breath);
            Add(m, "Right Arm Down-Up", 0.03f * breath);
            Add(m, "Head Turn Left-Right", 0.18f * Mathf.Sin(ph));
            Add(m, "Head Nod Down-Up", 0.05f * Mathf.Sin(ph * 2f + 1f));
            Add(m, "Head Tilt Left-Right", 0.06f * Mathf.Sin(ph + 0.5f));
            // weight shift from foot to foot
            float shift = Mathf.Sin(ph);
            Set(m, "Left Lower Leg Stretch", 0.93f + 0.05f * shift);
            Set(m, "Right Lower Leg Stretch", 0.93f - 0.05f * shift);
            y = 0.995f + 0.004f * breath;
        }

        static void WalkPose(float t, float[] m, ref float y)
        {
            float ph = t * Mathf.PI * 2f;
            float s = Mathf.Sin(ph), c = Mathf.Cos(ph);
            Set(m, "Left Upper Leg Front-Back", 0.5f - 0.42f * s);
            Set(m, "Right Upper Leg Front-Back", 0.5f + 0.42f * s);
            Set(m, "Left Lower Leg Stretch", 0.95f - 0.75f * Mathf.Pow(Mathf.Max(0f, c), 1.5f));
            Set(m, "Right Lower Leg Stretch", 0.95f - 0.75f * Mathf.Pow(Mathf.Max(0f, -c), 1.5f));
            Set(m, "Left Foot Up-Down", 0.15f * Mathf.Max(0f, c));
            Set(m, "Right Foot Up-Down", 0.15f * Mathf.Max(0f, -c));
            Set(m, "Left Arm Front-Back", 0.35f - 0.3f * s);
            Set(m, "Right Arm Front-Back", 0.35f + 0.3f * s);
            Both(m, "Forearm Stretch", -0.1f);
            Add(m, "Chest Twist Left-Right", 0.1f * s);
            Add(m, "Spine Twist Left-Right", -0.06f * s);
            Add(m, "Spine Front-Back", 0.05f);
            Add(m, "Head Nod Down-Up", 0.03f);
            y = 0.985f + 0.015f * Mathf.Cos(ph * 2f);
        }

        static void RunPose(float t, float[] m, ref float y)
        {
            float ph = t * Mathf.PI * 2f;
            float s = Mathf.Sin(ph), c = Mathf.Cos(ph);
            Set(m, "Left Upper Leg Front-Back", Mathf.Min(1f, 0.36f - 0.66f * s));
            Set(m, "Right Upper Leg Front-Back", Mathf.Min(1f, 0.36f + 0.66f * s));
            Set(m, "Left Lower Leg Stretch", 0.8f - 1.25f * Mathf.Pow(Mathf.Max(0f, c), 1.2f));
            Set(m, "Right Lower Leg Stretch", 0.8f - 1.25f * Mathf.Pow(Mathf.Max(0f, -c), 1.2f));
            Set(m, "Left Foot Up-Down", 0.2f * Mathf.Max(0f, c));
            Set(m, "Right Foot Up-Down", 0.2f * Mathf.Max(0f, -c));
            Both(m, "Arm Down-Up", -0.4f);
            Set(m, "Left Arm Front-Back", 0.35f - 0.55f * s);
            Set(m, "Right Arm Front-Back", 0.35f + 0.55f * s);
            Both(m, "Forearm Stretch", -0.1f);
            Fingers(m, -0.6f);
            Set(m, "Spine Front-Back", 0.2f);
            Set(m, "Chest Front-Back", 0.06f);
            Add(m, "Head Nod Down-Up", 0.15f);
            Add(m, "Chest Twist Left-Right", 0.16f * s);
            Add(m, "Spine Twist Left-Right", -0.08f * s);
            y = 1.035f - 0.032f * Mathf.Cos(ph * 2f);
        }

        static void JumpPose(float t, float[] m, ref float y)
        {
            float k = Smooth(Mathf.Clamp01(t * 2.2f));
            Lerp(m, "Left Upper Leg Front-Back", 0.6f, -0.3f, k);
            Lerp(m, "Left Lower Leg Stretch", 0.95f, -0.1f, k);
            Lerp(m, "Right Upper Leg Front-Back", 0.6f, 0.85f, k);
            Lerp(m, "Right Lower Leg Stretch", 0.95f, 0.6f, k);
            Lerp(m, "Left Arm Down-Up", -0.55f, 0.55f, k);
            Lerp(m, "Right Arm Down-Up", -0.55f, 0.35f, k);
            Lerp(m, "Left Arm Front-Back", 0.35f, 0.7f, k);
            Lerp(m, "Right Arm Front-Back", 0.35f, 0.1f, k);
            Both(m, "Forearm Stretch", 0.6f);
            Lerp(m, "Spine Front-Back", 0f, -0.15f, k);
            Lerp(m, "Head Nod Down-Up", 0f, 0.2f, k);
            Set(m, "Jaw Close", Mathf.Lerp(1f, 0.4f, k));
        }

        static void FallPose(float t, float[] m, ref float y)
        {
            float ph = t * Mathf.PI * 2f;
            // windmilling arms and pedalling legs – granny style
            Set(m, "Left Arm Down-Up", 0.5f + 0.3f * Mathf.Sin(ph));
            Set(m, "Right Arm Down-Up", 0.5f + 0.3f * Mathf.Sin(ph + Mathf.PI));
            Set(m, "Left Arm Front-Back", 0.4f + 0.55f * Mathf.Cos(ph));
            Set(m, "Right Arm Front-Back", 0.4f + 0.55f * Mathf.Cos(ph + Mathf.PI));
            Both(m, "Forearm Stretch", 0.7f);
            Set(m, "Left Upper Leg Front-Back", 0.25f - 0.35f * Mathf.Sin(ph));
            Set(m, "Right Upper Leg Front-Back", 0.25f + 0.35f * Mathf.Sin(ph));
            Set(m, "Left Lower Leg Stretch", 0.35f + 0.4f * Mathf.Cos(ph));
            Set(m, "Right Lower Leg Stretch", 0.35f - 0.4f * Mathf.Cos(ph));
            Set(m, "Spine Front-Back", 0.05f);
            Set(m, "Head Nod Down-Up", -0.15f);
            Set(m, "Jaw Close", 0.1f); // "aaaah!"
            Fingers(m, 0.8f);
        }

        static void TuckPose(float t, float[] m, ref float y)
        {
            Both(m, "Upper Leg Front-Back", -0.85f);
            Both(m, "Lower Leg Stretch", -0.75f);
            Both(m, "Arm Front-Back", 0.95f);
            Both(m, "Arm Down-Up", -0.25f);
            Both(m, "Forearm Stretch", -0.35f);
            Set(m, "Spine Front-Back", 0.55f);
            Set(m, "Chest Front-Back", 0.45f);
            Set(m, "Head Nod Down-Up", -0.35f);
            Fingers(m, -0.7f);
        }

        static void LandPose(float t, float[] m, ref float y)
        {
            float c = t < 0.25f ? Smooth(t / 0.25f) : 1f - Smooth((t - 0.25f) / 0.75f);
            c *= 0.7f;
            Both(m, "Upper Leg Front-Back", 0.6f - 0.4f * c);
            Both(m, "Lower Leg Stretch", 0.95f - 0.8f * c);
            Set(m, "Spine Front-Back", 0.35f * c);
            Both(m, "Arm Down-Up", -0.55f + 0.35f * c);
            Both(m, "Arm Front-Back", 0.35f + 0.3f * c);
            y = 1f - 0.13f * c;
        }

        // ------------------------------------------------------------ helpers
        static void BasePose(float[] m)
        {
            Array.Clear(m, 0, m.Length);
            Both(m, "Upper Leg Front-Back", 0.6f);
            Both(m, "Lower Leg Stretch", 0.95f);
            Both(m, "Arm Down-Up", -0.55f);
            Both(m, "Arm Front-Back", 0.35f);
            Both(m, "Forearm Stretch", 0.55f);
            Set(m, "Jaw Close", 1f);
            Fingers(m, 0.25f);
        }

        static int Index(string muscle)
        {
            int i = Array.IndexOf(HumanTrait.MuscleName, muscle);
            if (i < 0) throw new ArgumentException("Unknown muscle " + muscle);
            return i;
        }

        static void Set(float[] m, string muscle, float v) => m[Index(muscle)] = v;
        static void Add(float[] m, string muscle, float v) => m[Index(muscle)] += v;
        static void Both(float[] m, string muscle, float v) { Set(m, "Left " + muscle, v); Set(m, "Right " + muscle, v); }
        static void Lerp(float[] m, string muscle, float a, float b, float k) => Set(m, muscle, Mathf.Lerp(a, b, k));
        static float Smooth(float x) => x * x * (3f - 2f * x);

        static void Fingers(float[] m, float stretched)
        {
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string n = HumanTrait.MuscleName[i];
                if ((n.StartsWith("Left ") || n.StartsWith("Right ")) && n.EndsWith("Stretched")) m[i] = stretched;
            }
        }

        /// <summary>Animator property name for a muscle (finger muscles use "LeftHand.Index.1 Stretched").</summary>
        static string PropertyName(int i)
        {
            string n = HumanTrait.MuscleName[i];
            if (i < 55) return n;
            string side = n.StartsWith("Left ") ? "LeftHand" : "RightHand";
            string rest = n.Substring(n.IndexOf(' ') + 1);           // "Index 1 Stretched" / "Index Spread"
            string[] parts = rest.Split(' ');
            return parts.Length == 3
                ? $"{side}.{parts[0]}.{parts[1]} {parts[2]}"           // LeftHand.Index.1 Stretched
                : $"{side}.{parts[0]}.{parts[1]}";                      // LeftHand.Index.Spread
        }

        static AnimationClip MakeClip(string name, float length, bool loop, Pose pose)
        {
            int count = HumanTrait.MuscleCount;
            int frames = Mathf.Max(2, Mathf.RoundToInt(length * FrameRate)) + 1;
            var keys = new Keyframe[count][];
            for (int i = 0; i < count; i++) keys[i] = new Keyframe[frames];
            var rootY = new Keyframe[frames];
            var m = new float[count];

            for (int f = 0; f < frames; f++)
            {
                float t = f / (float)(frames - 1);
                BasePose(m);
                float y = 1f;
                pose(t, m, ref y);
                float time = t * length;
                for (int i = 0; i < count; i++) keys[i][f] = new Keyframe(time, m[i]);
                rootY[f] = new Keyframe(time, y);
            }

            var clip = new AnimationClip { name = name, frameRate = FrameRate };
            for (int i = 0; i < count; i++)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), PropertyName(i)), Smoothed(keys[i]));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x"), AnimationCurve.Constant(0, length, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y"), Smoothed(rootY));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.z"), AnimationCurve.Constant(0, length, 0.02f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.x"), AnimationCurve.Constant(0, length, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.y"), AnimationCurve.Constant(0, length, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.z"), AnimationCurve.Constant(0, length, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.w"), AnimationCurve.Constant(0, length, 1f));

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string path = $"{Folder}/{name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static AnimationCurve Smoothed(Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        static void BuildController(AnimationClip idle, AnimationClip walk, AnimationClip run,
            AnimationClip jump, AnimationClip fall, AnimationClip tuck, AnimationClip land)
        {
            string path = $"{Folder}/Granny.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            ctrl.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("DoubleJump", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var locomotion = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 2.2f);
            tree.AddChild(run, 5.5f);
            tree.AddChild(run, 8.5f);
            var children = tree.children;
            children[1].timeScale = 1.1f;
            children[3].timeScale = 1.3f;
            tree.children = children;
            sm.defaultState = locomotion;

            var jumpState = sm.AddState("JumpUp");  jumpState.motion = jump;
            var fallState = sm.AddState("Fall");    fallState.motion = fall;
            var tuckState = sm.AddState("Tuck");    tuckState.motion = tuck;
            var landState = sm.AddState("Land");    landState.motion = land;

            var anyJump = sm.AddAnyStateTransition(jumpState);
            Configure(anyJump, 0.08f);
            anyJump.canTransitionToSelf = true;
            anyJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            var anyTuck = sm.AddAnyStateTransition(tuckState);
            Configure(anyTuck, 0.08f);
            anyTuck.canTransitionToSelf = false;
            anyTuck.AddCondition(AnimatorConditionMode.If, 0, "DoubleJump");

            var toFall = locomotion.AddTransition(fallState);
            Configure(toFall, 0.25f);
            toFall.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            toFall.AddCondition(AnimatorConditionMode.Less, -3f, "VerticalSpeed");

            var jumpToFall = jumpState.AddTransition(fallState);
            Configure(jumpToFall, 0.35f);
            jumpToFall.AddCondition(AnimatorConditionMode.Less, 1.5f, "VerticalSpeed");
            jumpToFall.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");

            var tuckToFall = tuckState.AddTransition(fallState);
            Configure(tuckToFall, 0.25f, 1f);

            foreach (var air in new[] { jumpState, fallState, tuckState })
            {
                var toLand = air.AddTransition(landState);
                Configure(toLand, 0.06f);
                toLand.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
                toLand.AddCondition(AnimatorConditionMode.Less, 2.5f, "Speed");
                if (air == jumpState) toLand.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalSpeed");

                var toLoco = air.AddTransition(locomotion);
                Configure(toLoco, 0.15f);
                toLoco.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
                toLoco.AddCondition(AnimatorConditionMode.Greater, 2.5f, "Speed");
                if (air == jumpState) toLoco.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalSpeed");
            }

            var landToLoco = landState.AddTransition(locomotion);
            Configure(landToLoco, 0.2f, 0.75f);
            var landInterrupt = landState.AddTransition(locomotion);
            Configure(landInterrupt, 0.15f);
            landInterrupt.AddCondition(AnimatorConditionMode.Greater, 2.5f, "Speed");

            EditorUtility.SetDirty(ctrl);
        }

        static void Configure(AnimatorStateTransition t, float duration, float exitTime = -1f)
        {
            t.duration = duration;
            t.hasFixedDuration = true;
            t.hasExitTime = exitTime >= 0f;
            if (exitTime >= 0f) t.exitTime = exitTime;
        }
    }
}
