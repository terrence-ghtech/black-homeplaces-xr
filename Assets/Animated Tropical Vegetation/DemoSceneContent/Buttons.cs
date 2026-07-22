using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Buttons : MonoBehaviour
{
	public Camera _camera;
	public GameObject bamboo;
	public GameObject bananaTree;
	public GameObject bigPalmTree;
	public GameObject bushyPalm;
	public GameObject deadTree;
	public GameObject doublePalmTree;
	public GameObject jungleBushes;
	public GameObject jungleTree;
	public GameObject largePalmTree;
	public GameObject palmTree;
	public GameObject smallPalmTree;
	public GameObject smallTree;
	public GameObject tallTree;
	public GameObject tropicalPlant;
	
	public void Bamboos()
	{
		
		_camera.transform.position = new Vector3(98.62f, 2.7f, 70.4f);
		bamboo.transform.rotation = Quaternion.identity;
		bamboo.SetActive(true);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
		public void BananaTrees()
	{
		_camera.transform.position = new Vector3(100.19f, 2.3f, 64.5f);
		bananaTree.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(true);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
	
		public void BigPalmTree()
	{
		_camera.transform.position = new Vector3(100.04f, 8.3f, 61.85f);
		bigPalmTree.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(true);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
	
		public void BushyPalms()
	{
		_camera.transform.position = new Vector3(99.1f, 1.23f, 71.9f);
		bushyPalm.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(true);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
		public void DeadTrees()
	{
		_camera.transform.position = new Vector3(99.38f, 3.1f, 66.68f);
		deadTree.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(true);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
		
		public void DoublePalmTree()
	{
		_camera.transform.position = new Vector3(99.38f, 7.4f, 62.63f);
		doublePalmTree.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(true);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
	
		public void JungleBushes()
	{
		_camera.transform.position = new Vector3(98.83f, 1.85f, 72.4f);
		jungleBushes.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(true);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
	
		public void JungleTree()
	{
		_camera.transform.position = new Vector3(99.7f, 7.48f, 62.92f);
		jungleTree.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(true);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
	
		public void LargePalmTrees()
	{
		_camera.transform.position = new Vector3(102.65f, 10.3f, 55.56f);
		largePalmTree.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(true);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
	
		public void PalmTrees()
	{
		_camera.transform.position = new Vector3(98.5f, 5.6f, 59.1f);
		palmTree.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(true);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
	
		public void SmallPalmTrees()
	{
		_camera.transform.position = new Vector3(96.96f, 3.25f, 68.1f);
		smallPalmTree.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(true);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
		public void SmallTrees()
	{
		_camera.transform.position = new Vector3(99.21f, 2.98f, 68.1f);
		smallTree.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(true);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(false);
	}
		public void TallTrees()
	{
		_camera.transform.position = new Vector3(100.3f, 8.1f, 56.89f);
		tallTree.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(true);
		tropicalPlant.SetActive(false);
	}
		public void TropicalPlant()
	{
		_camera.transform.position = new Vector3(99.7f, 1.0f, 75.15f);
		tropicalPlant.transform.rotation = Quaternion.identity;
		bamboo.SetActive(false);
		bananaTree.SetActive(false);
		bigPalmTree.SetActive(false);
		bushyPalm.SetActive(false);
		doublePalmTree.SetActive(false);
		deadTree.SetActive(false);
		jungleBushes.SetActive(false);
		jungleTree.SetActive(false);
		largePalmTree.SetActive(false);
		palmTree.SetActive(false);
		smallPalmTree.SetActive(false);
		smallTree.SetActive(false);
		tallTree.SetActive(false);
		tropicalPlant.SetActive(true);
	}
	
}
