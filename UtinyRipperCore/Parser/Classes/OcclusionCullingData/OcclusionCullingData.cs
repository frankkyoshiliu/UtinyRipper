﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UtinyRipper.AssetExporters;
using UtinyRipper.Classes.OcclusionCullingDatas;
using UtinyRipper.Exporter.YAML;
using UtinyRipper.SerializedFiles;

namespace UtinyRipper.Classes
{
	public sealed class OcclusionCullingData : NamedObject
	{
		public OcclusionCullingData(AssetInfo assetInfo):
			base(assetInfo)
		{
		}

		public OcclusionCullingData(VirtualSerializedFile file):
			this(file.CreateAssetInfo(ClassIDType.OcclusionCullingData))
		{
			Name = nameof(OcclusionCullingData);

			file.AddAsset(this);
		}

		/// <summary>
		/// Not Release
		/// </summary>
		public static bool IsReadStaticRenderers(TransferInstructionFlags flags)
		{
			return !flags.IsSerializeGameRelease();
		}

		private static SceneObjectIdentifier CreateObjectID(IExportContainer container, Object asset)
		{
			long lid = (long)container.GetExportID(asset);
			return new SceneObjectIdentifier(lid, 0);
		}

		public void Initialize(IExportContainer container, OcclusionCullingSettings cullingSetting)
		{
			m_PVSData = (byte[])cullingSetting.PVSData;
			int renderCount = cullingSetting.StaticRenderers.Count;
			int portalCount = cullingSetting.Portals.Count;
			OcclusionScene scene = new OcclusionScene(cullingSetting.SceneGUID, renderCount, portalCount);
			m_scenes = new OcclusionScene[] { scene };

			m_staticRenderers = new SceneObjectIdentifier[scene.SizeRenderers];
			m_portals = new SceneObjectIdentifier[scene.SizePortals];
			SetIDs(container, cullingSetting, scene);
		}

		public override void Read(AssetStream stream)
		{
			base.Read(stream);

			m_PVSData = stream.ReadByteArray();
			stream.AlignStream(AlignType.Align4);

			m_scenes = stream.ReadArray<OcclusionScene>();
			if (IsReadStaticRenderers(stream.Flags))
			{
				m_staticRenderers = stream.ReadArray<SceneObjectIdentifier>();
				m_portals = stream.ReadArray<SceneObjectIdentifier>();
			}
		}

		protected override YAMLMappingNode ExportYAMLRoot(IExportContainer container)
		{
			YAMLMappingNode node = base.ExportYAMLRoot(container);
			node.Add("m_PVSData", PVSData.ExportYAML());
			node.Add("m_Scenes", Scenes.ExportYAML(container));

			SetExportData(container);
			node.Add("m_StaticRenderers", StaticRenderers.ExportYAML(container));
			node.Add("m_Portals", Portals.ExportYAML(container));
			return node;
		}

		private void SetExportData(IExportContainer container)
		{
			// if < 3.0.0 this asset doesn't exist

			// 3.0.0 to 5.5.0 this asset is created by culling settings so it has set data already
			if(OcclusionCullingSettings.IsReadPVSData(container.Version))
			{
				return;
			}

			// if >= 5.5.0 and !Release this asset containts renderer data
			if (IsReadStaticRenderers(container.Flags))
			{
				return;
			}

			// if >= 5.5.0 and Release this asset doesn't containt renderers data so we need to create it
			List<OcclusionCullingSettings> cullingSettings = new List<OcclusionCullingSettings>();
			foreach (ISerializedFile file in File.Collection.Files)
			{
				foreach(Object asset in file.FetchAssets())
				{
					if(asset.ClassID == ClassIDType.OcclusionCullingSettings)
					{
						OcclusionCullingSettings cullingSetting = (OcclusionCullingSettings)asset;
						if (Scenes.Any(t => t.Scene == cullingSetting.SceneGUID))
						{
							cullingSettings.Add(cullingSetting);
						}
					}
				}
			}

			int maxRenderer = Scenes.Max(j => j.IndexRenderers);
			OcclusionScene rscene = Scenes.First(t => t.IndexRenderers == maxRenderer);
			m_staticRenderers = new SceneObjectIdentifier[rscene.IndexRenderers + rscene.SizeRenderers];

			int maxPortal = Scenes.Max(j => j.IndexPortals);
			OcclusionScene pscene = Scenes.First(t => t.IndexPortals == maxPortal);
			m_portals = new SceneObjectIdentifier[pscene.IndexPortals + pscene.SizePortals];

			foreach(OcclusionCullingSettings cullingSetting in cullingSettings)
			{
				OcclusionScene scene = Scenes.First(t => t.Scene == cullingSetting.SceneGUID);
				if (scene.SizeRenderers != cullingSetting.StaticRenderers.Count)
				{
					throw new Exception($"Scene renderer count {scene.SizeRenderers} doesn't match with given {cullingSetting.StaticRenderers.Count}");
				}
				if (scene.SizePortals != cullingSetting.Portals.Count)
				{
					throw new Exception($"Scene portal count {scene.SizePortals} doesn't match with given {cullingSetting.Portals.Count}");
				}
				SetIDs(container, cullingSetting, scene);
			}
		}

		private void SetIDs(IExportContainer container, OcclusionCullingSettings cullingSetting, OcclusionScene scene)
		{
			for (int i = 0; i < cullingSetting.StaticRenderers.Count; i++)
			{
				PPtr<Renderer> prenderer = cullingSetting.StaticRenderers[i];
				Renderer renderer = prenderer.GetAsset(cullingSetting.File);
				m_staticRenderers[scene.IndexRenderers + i] = CreateObjectID(container, renderer);
			}

			for (int i = 0; i < cullingSetting.Portals.Count; i++)
			{
				PPtr<OcclusionPortal> pportal = cullingSetting.Portals[i];
				OcclusionPortal portal = pportal.GetAsset(cullingSetting.File);
				m_portals[scene.IndexPortals + i] = CreateObjectID(container, portal);
			}
		}

		public override string ExportName => Path.Combine(AssetsKeyWord, OcclusionCullingSettings.SceneKeyWord, ClassID.ToString());

		public IReadOnlyList<byte> PVSData => m_PVSData;
		public IReadOnlyList<OcclusionScene> Scenes => m_scenes;
		public IReadOnlyList<SceneObjectIdentifier> StaticRenderers => m_staticRenderers;
		public IReadOnlyList<SceneObjectIdentifier> Portals => m_portals;
		
		private byte[] m_PVSData;
		private OcclusionScene[] m_scenes;
		private SceneObjectIdentifier[] m_staticRenderers = new SceneObjectIdentifier[0];
		private SceneObjectIdentifier[] m_portals = new SceneObjectIdentifier[0];
	}
}
