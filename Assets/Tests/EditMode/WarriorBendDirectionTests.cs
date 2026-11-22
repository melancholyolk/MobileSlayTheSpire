using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.IK;
public class WarriorBendDirectionTests
{
    [TestCase("ForearmLIK",1)] [TestCase("ForearmRIK",1)]
    [TestCase("ShinLIK",-1)] [TestCase("ShinRIK",-1)]
    public void SideViewLimbKeepsAnatomicalBendWhenMirrored(string name,int expected)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameResources/Characters/0/Skin/13000.prefab");
        var go=Object.Instantiate(prefab);go.SetActive(false);
        try
        {
            var solver=go.GetComponentsInChildren<LimbSolver2D>(true).Single(s=>s.name==name);
            solver.Initialize(); var chain=solver.GetChain(0); var space=solver.transform.parent;
            var start=space.InverseTransformPoint(chain.transforms[0].position);
            var end=space.InverseTransformPoint(chain.effector.position);
            foreach(float mirror in new[]{1f,-1f})
            {
                go.transform.localScale=new Vector3(mirror,1,1);
                foreach(float compression in new[]{.65f,.8f,.95f})
                {
                    chain.target.position=space.TransformPoint(Vector3.Lerp(start,end,compression));
                    solver.UpdateIK(1);
                    var a=(Vector2)space.InverseTransformPoint(chain.transforms[1].position)-(Vector2)start;
                    var b=(Vector2)space.InverseTransformPoint(chain.effector.position)-(Vector2)start;
                    Assert.Greater((a.x*b.y-a.y*b.x)*expected,.001f,name+" reversed bend");
                    Assert.Less(Vector3.Distance(chain.effector.position,chain.target.position),.02f);
                }
            }
        }
        finally {Object.DestroyImmediate(go);}
    }
}
