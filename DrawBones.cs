using UnityEngine;

public class DrawBones : MonoBehaviour
{
    public Color boneColor = Color.cyan;
    public bool depthTest = false;
    // empty Start to get the "enabled" checkbox in the editor
    private void Start() { }
    private void OnDrawGizmos()
    {
        if (enabled)
            DrawBonesRec(transform);
    }
    Vector3 DrawBonesRec(Transform aRoot)
    {
        Vector3 pos = aRoot.position;
        foreach(Transform t in aRoot)
        {
            Debug.DrawLine(pos, DrawBonesRec(t),boneColor,0, depthTest);
        }
        return pos;
    }
}
