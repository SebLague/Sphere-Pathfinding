using System.Collections.Generic;
using UnityEngine;

public class Pathfinder : MonoBehaviour {

    public int gridResolution;
    public LayerMask obstacleMask;
    public float nodeRadius = .5f;

    [Header ("Path Test")]
    public Transform testA;
    public Transform testB;
    public bool showNodes;
    public bool showTestPath;
    List<Vector3> testPath;

    Grid[] grids;
    Dictionary<Vector3Int, Node> edgeDictionary;

    void Start () {
        CreateGrid ();
    }

    public List<Vector3> FindPath (Vector3 startPos, Vector3 targetPos) {
        Node startNode = GetClosestNodeFromWorldPoint (targetPos);
        Node targetNode = GetClosestNodeFromWorldPoint (startPos);

        if (!startNode.walkable || !targetNode.walkable) {
            return null;
        }

        // TODO: Optimize
        Heap<Node> openSet = new Heap<Node> (gridResolution * gridResolution * 6);
        HashSet<Node> closedSet = new HashSet<Node> ();
        openSet.Add (startNode);

        while (openSet.Count > 0) {
            Node currentNode = openSet.RemoveFirst ();
            closedSet.Add (currentNode);

            if (currentNode == targetNode) {
                return CreatePathPoints (startNode, targetNode);
            }

            for (int i = 0; i < currentNode.neighbours.Count; i++) {
                Node neighbour = currentNode.neighbours[i];
                if (closedSet.Contains (neighbour)) {
                    continue;
                }

                float newMovementCostToNeighbour = currentNode.gCost + currentNode.neighbourDst[i];
                if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains (neighbour)) {
                    neighbour.gCost = newMovementCostToNeighbour;
                    neighbour.hCost = GetDistanceOnSphere (neighbour.position, targetNode.position);
                    neighbour.parent = currentNode;

                    if (!openSet.Contains (neighbour)) {
                        openSet.Add (neighbour);
                    } else {
                        //openSet.UpdateItem(neighbour);
                    }
                }
            }
        }

        Debug.Log ("No path found");
        return null;
    }

    List<Vector3> CreatePathPoints (Node startNode, Node endNode) {
        List<Vector3> path = new List<Vector3> ();
        Node currentNode = endNode;

        while (currentNode != startNode) {
            path.Add (currentNode.position);
            currentNode = currentNode.parent;
        }
        return path;

    }

    void CreateGrid () {
        edgeDictionary = new Dictionary<Vector3Int, Node> ();
        Vector3[] directions = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        grids = new Grid[directions.Length];
        for (int i = 0; i < directions.Length; i++) {
            grids[i] = CreateSubGrid (directions[i]);
        }
    }

    Grid CreateSubGrid (Vector3 localUp) {
        float maxRadius = 1000;
        Vector3 axisA = new Vector3 (localUp.y, localUp.z, localUp.x);
        Vector3 axisB = Vector3.Cross (localUp, axisA);
        Node[, ] nodes = new Node[gridResolution, gridResolution];
        float border = 0f;

        for (int y = 0; y < gridResolution; y++) {
            for (int x = 0; x < gridResolution; x++) {
                Vector2 percent = new Vector2 (x, y) / (gridResolution - 1);
                percent = new Vector2 (Mathf.Lerp (border, 1 - border, percent.x), Mathf.Lerp (border, 1 - border, percent.y));

                Vector3 pointOnUnitCube = localUp + (percent.x - .5f) * 2 * axisA + (percent.y - .5f) * 2 * axisB;

                // Distribute on sphere
                float x2 = pointOnUnitCube.x * pointOnUnitCube.x;
                float y2 = pointOnUnitCube.y * pointOnUnitCube.y;
                float z2 = pointOnUnitCube.z * pointOnUnitCube.z;

                float sx = pointOnUnitCube.x * Mathf.Sqrt (1f - y2 / 2f - z2 / 2f + y2 * z2 / 3f);
                float sy = pointOnUnitCube.y * Mathf.Sqrt (1f - x2 / 2f - z2 / 2f + x2 * z2 / 3f);
                float sz = pointOnUnitCube.z * Mathf.Sqrt (1f - x2 / 2f - y2 / 2f + x2 * y2 / 3f);
                Vector3 pointOnUnitSphere = new Vector3 (sx, sy, sz);

                // Raycast for collisions:
                Ray ray = new Ray (pointOnUnitSphere * maxRadius, -pointOnUnitSphere);
                RaycastHit hit;

                if (Physics.SphereCast (ray, nodeRadius, out hit)) {
                    bool walkable = obstacleMask != (obstacleMask | (1 << hit.collider.gameObject.layer));
                    Node node = new Node ();
                    node.position = hit.point;
                    node.walkable = walkable;
                    nodes[x, y] = node;
                } else {
                    Debug.LogError ("No collision");
                }
            }
        }

        // Add neighbours
        for (int y = 0; y < gridResolution; y++) {
            for (int x = 0; x < gridResolution; x++) {
                for (int offsetY = -1; offsetY <= 1; offsetY++) {
                    for (int offsetX = -1; offsetX <= 1; offsetX++) {
                        if (offsetX != 0 || offsetY != 0) {
                            int nX = x + offsetX;
                            int nY = y + offsetY;
                            if (nX >= 0 && nX < gridResolution && nY >= 0 && nY < gridResolution) {
                                if (nodes[nX, nY].walkable) {
                                    nodes[x, y].AddNeighbour (nodes[nX, nY]);
                                }
                            }
                        }
                    }
                }

                // Handle edge nodes which need to link to neighbouring grids
                // TODO: Use better method, this is gross and slow
                if (x == 0 || x == gridResolution - 1 || y == 0 || y == gridResolution - 1) {
                    Vector2 percent = new Vector2 (x, y) / (gridResolution - 1);
                    Vector3 p = localUp + (percent.x - .5f) * 2 * axisA + (percent.y - .5f) * 2 * axisB;
                    p *= gridResolution;
                    Vector3Int coordOnUnitCube = new Vector3Int ((int) p.x, (int) p.y, (int) p.z);

                    if (nodes[x, y].walkable) {
                        if (edgeDictionary.ContainsKey (coordOnUnitCube)) {
                            var neighbour = edgeDictionary[coordOnUnitCube];
                            if (neighbour.walkable) {
                                neighbour.AddNeighbour (nodes[x, y]);
                                nodes[x, y].AddNeighbour (neighbour);
                            }
                        } else {
                            edgeDictionary.Add (coordOnUnitCube, nodes[x, y]);
                        }
                    }
                }
            }
        }

        return new Grid () { nodes = nodes };
    }

    // TODO: calculate arc length
    float GetDistanceOnSphere (Vector3 a, Vector3 b) {
        return (a - b).magnitude;
    }

    Node GetClosestNodeFromWorldPoint (Vector3 p) {
        // TODO: Replace this with direct calculation of grid index from point
        float closest = float.MaxValue;
        Node closestNode = null;
        foreach (var g in grids) {
            for (int y = 0; y < g.nodes.GetLength (1); y++) {
                for (int x = 0; x < g.nodes.GetLength (0); x++) {
                    var n = g.nodes[x, y];
                    float sqrDst = (p - n.position).sqrMagnitude;
                    if (sqrDst < closest) {
                        closest = sqrDst;
                        closestNode = n;
                    }
                }
            }
        }
        return closestNode;
    }

    class Grid {
        public Node[, ] nodes;
    }

    class Node : IHeapItem<Node> {
        public Vector3 position;
        public bool walkable;
        public List<Node> neighbours;
        public List<float> neighbourDst;

        public Node parent;
        int heapIndex;
        public float gCost;
        public float hCost;
        public float fCost {
            get {
                return gCost + hCost;
            }
        }

        public Node () {
            neighbourDst = new List<float> ();
            neighbours = new List<Node> ();
        }

        public void AddNeighbour (Node node) {
            float straightLineDst = (position - node.position).magnitude;
            neighbours.Add (node);
            neighbourDst.Add (straightLineDst);
        }

        public int HeapIndex {
            get {
                return heapIndex;
            }
            set {
                heapIndex = value;
            }
        }

        public int CompareTo (Node nodeToCompare) {
            int compare = fCost.CompareTo (nodeToCompare.fCost);
            if (compare == 0) {
                compare = hCost.CompareTo (nodeToCompare.hCost);
            }
            return -compare;
        }

    }

    void OnDrawGizmos () {
        if (showNodes) {
            if (grids != null) {
                foreach (var g in grids) {
                    for (int y = 0; y < g.nodes.GetLength (1); y++) {
                        for (int x = 0; x < g.nodes.GetLength (0); x++) {
                            var n = g.nodes[x, y];
                            Gizmos.color = (n.walkable) ? Color.white : Color.red;
                            Gizmos.DrawSphere (n.position, .15f);
                        }
                    }
                }

                /* 
                Gizmos.color = Color.cyan;
                var node = GetClosestNodeFromWorldPoint (a.position);
                Gizmos.DrawSphere (node.position, .2f);
                Gizmos.color = Color.black;
                foreach (Node n in node.neighbours) {
                    Gizmos.DrawSphere (n.position, .2f);
                }
                */
            }
        }
    }
}