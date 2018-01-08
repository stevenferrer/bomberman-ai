﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Diagnostics;

public class GridScript : MonoBehaviour {

	public Bomb bombScript;
	public LayerMask unwalkableMask;
	public LayerMask playerCollisionMask;
	public Vector2 gridWorldSize;
	public float nodeRaduis;

	Node[,] grid;
	float nodeDiameter;
	int gridSizeX,gridSizeY;

	private bool isCreated = false;

	public static List<Vector3> Walls_Destroyed = new List<Vector3>();
	public static List<Vector3> Dropped_Bombs = new List<Vector3> ();
	public static List<Vector3> Exploded_Bombs = new List<Vector3> ();

	void Awake(){
		nodeDiameter = nodeRaduis * 2;
		gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
		gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
	}

	void Start(){
		CreateGrid ();
	}

	void Update(){
		if (isCreated == true) {
			UpdateWalls ();
			UpdateBombs ();
		}
	}

	public int MaxSize{
		get{
			return gridSizeX * gridSizeY; 
		}
	}

	public void CreateGrid(){
		if (isCreated == false) {
			Stopwatch sw = new Stopwatch ();
			sw.Start ();

			grid = new Node[gridSizeX, gridSizeY];
			Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.forward * gridWorldSize.y / 2;
			int index = 0;
			for (int x = 0; x < gridSizeX; x++) {
				for (int y = 0; y < gridSizeY; y++) {
					Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRaduis) + Vector3.forward * (y * nodeDiameter + nodeRaduis);
					bool walkable = !(Physics.CheckSphere (worldPoint, nodeRaduis, unwalkableMask));
					GameObject goDes = GetObjectByPosition (new Vector3 (worldPoint.x, 1, worldPoint.z), GameObjectType.DESTRUCTIBLE_WALL.GetTag());
					GameObject goBomb = GetObjectByPosition (new Vector3 (worldPoint.x, 1, worldPoint.z), GameObjectType.BOMB.GetTag());
					GameObject goAgent = GetObjectByPosition (new Vector3 (worldPoint.x, 1, worldPoint.z), GameObjectType.AGENT.GetTag());

					bool isDestructibleWall = (goDes == null) ? false : true;
					bool isBomb = (goBomb == null) ? false : true;
					bool isAgent = (goAgent == null) ? false : true;

					Node n = new Node (walkable, isDestructibleWall, worldPoint, x, y);
					n.isBomb = isBomb;
					n.HeapIndex = index++;
					if (isAgent)
						n.agentName = goAgent.name;
					
					grid [x, y] = n;
				}
			}
			isCreated = true;
			sw.Stop ();
			print ("Grid was successfully Created at " + sw.ElapsedMilliseconds+" ms");
		}else
			print ("Grid is already Created");
	}

	private void UpdateWalls(){
		if (GridScript.Walls_Destroyed.Count > 0) {
			foreach (Vector3 pos in GridScript.Walls_Destroyed) {
				Node wall = NodeFromWorldPoint (pos);
				grid [wall.gridX, wall.gridY].walkable = true;
				grid [wall.gridX, wall.gridY].destructible = false;
			}
			GridScript.Walls_Destroyed.Clear ();
		}
	}

	IEnumerator UpdateExplosion(Node bomb){
		List<Node> explosionRange = GetNeighbours (bomb, bombScript.bombRange);
		foreach (Node n in explosionRange) {
			grid [n.gridX, n.gridY].isBombRange = true;
		}
		yield return new WaitForSeconds (.05f);
	}


	private void UpdateBombs(){
		if (GridScript.Dropped_Bombs.Count > 0) {
			foreach (Vector3 pos in GridScript.Dropped_Bombs) {
				Node bomb = NodeFromWorldPoint (pos);
				GameObject go = GetObjectByPosition (pos, GameObjectType.BOMB.GetTag ());
				grid [bomb.gridX, bomb.gridY].isBomb = true;
				grid [bomb.gridX, bomb.gridY].walkable = false;
				StartCoroutine (UpdateExplosion (bomb));
			}
			GridScript.Dropped_Bombs.Clear ();
		}

		if (GridScript.Exploded_Bombs.Count > 0) {
			foreach (Vector3 pos in GridScript.Exploded_Bombs) {
				Node bomb = NodeFromWorldPoint (pos);
				grid [bomb.gridX, bomb.gridY].isBomb = false;
				grid [bomb.gridX, bomb.gridY].walkable = true;
			}
			GridScript.Exploded_Bombs.Clear ();
		}
	}

	public void UpdateAgentMoves(Vector3 current, Vector3 next,string agentName){
		Node currentNode = NodeFromWorldPoint (Utility.RoundToInt(current));
		Node nextNode = NodeFromWorldPoint (Utility.RoundToInt(next));

		if (next == Vector3.zero) {
			grid [currentNode.gridX, currentNode.gridY].agentName = agentName;
		} else {
			grid [currentNode.gridX, currentNode.gridY].agentName = agentName;
			grid [nextNode.gridX, nextNode.gridY].agentName = null;
		}

		print (grid [currentNode.gridX, currentNode.gridY].agentName);
	}

	public static void DestroyDestructible(Vector3 position){
		print (position.ToString ());
	}

	public List<Node> GetNeighbours(Node node){
		return GetNeighbours (node, 1, false,false);
	}

	public List<Node> GetNeighbours(Node node, int range){
		return GetNeighbours (node, range, false,false);
	}

	public List<Node> GetNeighbours(Node node,int range,bool diagonal){
		return GetNeighbours (node, range, diagonal, false);
	}

	public List<Node> GetNeighbours(Node node,int range,bool diagonal,bool borderOnly){
		List<Node> neighbours = new List<Node> ();
		bool yCheck = false;

		for (int x = -range; x <= range; x++) {
			for (int y = -range; y <= range; y++) {
				if (x == 0 && y == 0)
						continue;

				//for evaulation
				if (borderOnly) {
					yCheck =  (y == 0 || (y < 0 && y > -range) || (y > 0 && y < range));
					if ((x < 0 && x > -range) && yCheck)
						continue;
					if ((x > 0 && x < range) && yCheck)
						continue;
				}
				
				int checkX = node.gridX + x;
				int checkY = node.gridY + y;

				if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY) {
					if (diagonal == false) {
						if ((x == 0 && (y < 0 || y > 0)) || (y == 0 && (x < 0 || x > 0))) {
							neighbours.Add (grid [checkX, checkY]);
						}
					} else {
						//for evaulation
						if (borderOnly) {
							if (x == 0 && yCheck)
								continue;
						}
						neighbours.Add (grid [checkX, checkY]);
					}
				}
			}
		} 
			
		//Set directions
		if (diagonal == false) {
			List<Node>[] directions = new List<Node>[4]; //left,right,up,down
			for (int x = 0; x < directions.GetLength (0); x++)
				directions [x] = new List<Node> ();
		
			foreach (Node n in neighbours) {
				if (n.gridX == node.gridX) {
					if (n.gridY < node.gridY) {
						directions [0].Add (n); //left
					} else {
						directions [1].Add (n); //right ,reverse
					}
				} else if (n.gridY == node.gridY) {
					if (n.gridX < node.gridX) {
						directions [2].Add (n); //up
					} else {
						directions [3].Add (n); //down , reverse
					}
				}
			}

			neighbours.Clear ();

			//Eliminate walls (indestructible, destructible, and outerwall)
			for (int x = 0; x < directions.GetLength (0); x++) {
				if ((x % 2) == 0)
					directions [x].Reverse ();

				bool isWalk = false;
				for (int y = 0; y < directions [x].Count; y++) {
				
					if (isWalk)
						directions [x] [y].isBombRange = true;
					if (!directions [x] [y].walkable) {
						isWalk = true;
						break;
					}
					neighbours.Add (directions [x] [y]);
				}
			}

			neighbours.RemoveAll (s => s.isBombRange == true);
		}
		return neighbours;
	}
		
	public Node NodeFromWorldPoint(Vector3 worldPosition){
		float percentX = (worldPosition.x + gridWorldSize.x / 2) / gridWorldSize.x;
		float percentY = (worldPosition.z + gridWorldSize.y / 2) / gridWorldSize.y;
		percentX = Mathf.Clamp01 (percentX);
		percentY = Mathf.Clamp01 (percentY);

		int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
		int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);

		return grid [x, y];
	}

	private GameObject GetObjectByPosition(Vector3 position,string tag){
		GameObject[] gameObjects = GameObject.FindGameObjectsWithTag (tag);
		foreach (GameObject go in gameObjects) {
			if (go.transform.position == position) {
				return go;
			}
		}
		return null;
	}

	void OnDrawGizmos(){
		Gizmos.DrawWireCube (transform.position, new Vector3 (gridWorldSize.x, 1, gridWorldSize.y));
		if (grid != null) {
			foreach (Node n in grid) {
				Gizmos.color = (n.walkable) ? Color.white : Color.red;
				if (n.walkable == false && n.destructible == true)
					Gizmos.color = Color.blue;
				if (n.isBomb == true && n.walkable == false)
					Gizmos.color = Color.grey;
				if (n.agentName != null)
					Gizmos.color = Color.green;
				if (n.isBombRange == true)
					Gizmos.color = Color.magenta;
				
				Gizmos.DrawCube (n.worldPosition, Vector3.one * (nodeDiameter - .1f) );
			}
		}
	}

}
