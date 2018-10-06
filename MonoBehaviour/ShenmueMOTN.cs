using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shenmunity
{
    [ExecuteInEditMode]
    [SelectionBase]
    public class ShenmueMOTN : MonoBehaviour
    {
        [HideInInspector]
        public ShenmueAssetRef m_motn = new ShenmueAssetRef();
        [HideInInspector]
        public MOTN m_motnData;
        [HideInInspector]
        public int m_selectedSeq = -1;
        [HideInInspector]
        public float m_playPos;
        public ShenmueModel m_playTarget;
        public int m_boneOffset;
        public AnimationCurve[] m_curves;
        Dictionary<ShenmueTransform, Vector3> m_bindPose = new Dictionary<ShenmueTransform, Vector3>();

#if UNITY_EDITOR
        [MenuItem("GameObject/Shenmunity/Scene (MOTN)", priority = 10)]
        public static void Create()
        {
            var sm = new GameObject("Shenmue motion");
            TACFileSelector.SelectFile(TACReader.FileType.CHRT, sm.AddComponent<ShenmueMOTN>().m_motn);
        }
#endif

        public void OnChange()
        {
            if (string.IsNullOrEmpty(m_motn.m_path))
            {
                return;
            }

            uint len;
            using (var br = TACReader.GetBytes(m_motn.m_path, out len))
            {
                m_motnData = new MOTN(br);
            }
            m_selectedSeq = -1;
        }

        public void UpdateFrame()
        {
            if(m_selectedSeq != -1 && m_playTarget)
            {
                var seq = m_motnData.m_sequences[m_selectedSeq];
                var pose = seq.GetPose(m_playPos);
                var bones = CollectBones(m_playTarget);

                int boneCount = seq.m_boneIds.Count + 1;

                m_curves = new AnimationCurve[boneCount*3];
                for (int i = 0; i < boneCount*3; i++)
                {
                    m_curves[i] = new AnimationCurve();
                    foreach (var kf in seq.m_keyFrames[i])
                    {
                        m_curves[i].AddKey(kf.m_frame, kf.m_value0 + kf.m_value1 + kf.m_value2);
                    }
                }

                for (int b = 0; b < boneCount; b++)
                {
                    var bone = FindBone(b - m_boneOffset, bones);

                    if (!bone)
                        continue;

                    var t = bone.transform;
                    if (!m_bindPose.ContainsKey(bone))
                    {
                        m_bindPose[bone] = t.localEulerAngles;
                    }

                    t.localEulerAngles = Vector3.zero;// m_bindPose[bone];
                    t.Rotate(Vector3.forward, pose[b*3+2] * 360.0f);
                    t.Rotate(Vector3.up, pose[b*3+1] * 360.0f);
                    t.Rotate(Vector3.right, pose[b*3] * 360.0f);
                }
            }
        }

        ShenmueTransform FindBone(int i, List<ShenmueTransform> bones)
        {
            int[] boneIds = new int[] { 4, 5, 6 };
            if (i < 0 || i >= boneIds.Length)
                return null;

            int boneId = boneIds[i];
            if (boneId == -1)
                return null;

            var find = "(" + boneId + " 0 ";
            foreach(var b in bones)
            {
                if (b.name.Contains(find))
                    return b;
            }
            return null;
        }

        List<ShenmueTransform> CollectBones(ShenmueModel model)
        {
            var bones = new List<ShenmueTransform>();
            CollectBonesFromTransform(model.transform, bones);
            return bones;
        }

        void CollectBonesFromTransform(Transform t, List<ShenmueTransform> list)
        {
            var st = t.GetComponent<ShenmueTransform>();
            if (st)
            {
                if(!st.name.Contains(" 255 "))
                {
                    list.Add(st);
                }
            }
            foreach(Transform child in t.transform)
            {
                CollectBonesFromTransform(child, list);
            }
        }
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(ShenmueMOTN))]
    public class ShenmueMOTNEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var smar = (ShenmueMOTN)target;

            smar.m_motn.DoInspectorGUI(TACReader.FileType.MOTN, () => smar.OnChange());
            DrawDefaultInspector();

            if(smar.m_motnData != null)
            {
                int i = 0;
                foreach (var seq in smar.m_motnData.m_sequences)
                {
                    if(GUILayout.Toggle(i == smar.m_selectedSeq, seq.m_name))
                    {
                        if(smar.m_selectedSeq != i)
                        {
                            smar.m_selectedSeq = i;
                            smar.m_playPos = 0;
                        }
                    }
                    i++;
                }

                if(smar.m_selectedSeq != -1)
                {
                    float pos = GUILayout.HorizontalSlider(smar.m_playPos, 0, smar.m_motnData.m_sequences[smar.m_selectedSeq].m_flags);
                    if(pos != smar.m_playPos)
                    {
                        smar.m_playPos = pos;
                        smar.UpdateFrame();
                    }
                    GUILayout.Label((smar.m_playPos / 30.0f).ToString());
                    smar.m_boneOffset = (int)GUILayout.HorizontalSlider(smar.m_boneOffset, 0, 20);
                }
            }

        }
    }
#endif
}