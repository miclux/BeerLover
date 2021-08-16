﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace BeerLover
{
	public class Building_FermentingBarrel : Verse.Building
	{
		public ThingDef ContainingWortType { get { return _containingWortType; } }
		private ThingDef _containingWortType;
		private string _containingWortTypeDefName;

		public bool IsCompatibleWortType(ThingDef wortType)
		{
			return _containingWortType.Equals(wortType);
		}

		public void AddWort(Thing wort)
		{
			int num = Mathf.Min(wort.stackCount, 25 - this.wortCount);
			if (num > 0)
			{
				AddWort(num);
				_containingWortTypeDefName = wort.ThingID.Replace(Thing.IDNumberFromThingID(wort.ThingID).ToString(), string.Empty);
				_containingWortType = ThingDef.Named(_containingWortTypeDefName);

				wort.SplitOff(num).Destroy(DestroyMode.Vanish);
			}
		}

		public float Progress
		{
			get
			{
				return this.progressInt;
			}
			set
			{
				if (value == this.progressInt)
				{
					return;
				}
				this.progressInt = value;
				this.barFilledCachedMat = null;
			}
		}

		private Material BarFilledMat
		{
			get
			{
				if (this.barFilledCachedMat == null)
				{
					this.barFilledCachedMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.Lerp(Building_FermentingBarrel.BarZeroProgressColor, Building_FermentingBarrel.BarFermentedColor, this.Progress), false);
				}
				return this.barFilledCachedMat;
			}
		}

		public int SpaceLeftForWort
		{
			get
			{
				if (this.Fermented)
				{
					return 0;
				}
				return 25 - this.wortCount;
			}
		}

		public bool Empty
		{
			get
			{
				return this.wortCount <= 0;
			}
		}

		public bool Fermented
		{
			get
			{
				return !this.Empty && this.Progress >= 1f;
			}
		}

		private float CurrentTempProgressSpeedFactor
		{
			get
			{
				CompProperties_TemperatureRuinable compProperties = this.def.GetCompProperties<CompProperties_TemperatureRuinable>();
				float ambientTemperature = base.AmbientTemperature;
				if (ambientTemperature < compProperties.minSafeTemperature)
				{
					return 0.1f;
				}
				if (ambientTemperature < 7f)
				{
					return GenMath.LerpDouble(compProperties.minSafeTemperature, 7f, 0.1f, 1f, ambientTemperature);
				}
				return 1f;
			}
		}

		private float ProgressPerTickAtCurrentTemp
		{
			get
			{
				return 2.7777778E-06f * this.CurrentTempProgressSpeedFactor;
			}
		}

		private int EstimatedTicksLeft
		{
			get
			{
				return Mathf.Max(Mathf.RoundToInt((1f - this.Progress) / this.ProgressPerTickAtCurrentTemp), 0);
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look<int>(ref this.wortCount, "wortCount", 0, false);
			Scribe_Values.Look<float>(ref this.progressInt, "progress", 0f, false);
			Scribe_Values.Look<string>(ref this._containingWortTypeDefName, "containingWortTypeDefName");
			if (_containingWortTypeDefName != null)
				_containingWortType = ThingDef.Named(_containingWortTypeDefName);
		}

		public override void TickRare()
		{
			base.TickRare();
			if (!this.Empty)
			{
				this.Progress = Mathf.Min(this.Progress + 250f * this.ProgressPerTickAtCurrentTemp, 1f);
			}
		}

		public void AddWort(int count)
		{
			base.GetComp<CompTemperatureRuinable>().Reset();
			if (this.Fermented)
			{
				Log.Warning("Tried to add wort to a barrel full of beer. Colonists should take the beer first.");
				return;
			}
			int num = Mathf.Min(count, 25 - this.wortCount);
			if (num <= 0)
			{
				return;
			}
			this.Progress = GenMath.WeightedAverage(0f, (float)num, this.Progress, (float)this.wortCount);
			this.wortCount += num;
		}

		protected override void ReceiveCompSignal(string signal)
		{
			if (signal == "RuinedByTemperature")
			{
				this.Reset();
			}
		}

		private void Reset()
		{
			this.wortCount = 0;
			this.Progress = 0f;
		}

		public override string GetInspectString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(base.GetInspectString());
			var wortTranslation = "";
			if (_containingWortType != null)
				wortTranslation = _containingWortType.LabelCap;

			if (stringBuilder.Length != 0)
			{
				stringBuilder.AppendLine();
			}
			CompTemperatureRuinable comp = base.GetComp<CompTemperatureRuinable>();
			if (!this.Empty && !comp.Ruined)
			{
				if (this.Fermented)
				{
					var beerDef = GetBeerDefForWort(_containingWortType) ?? ThingDefOf.Beer;
					var beerTranslation = "";
					if (beerDef != null)
						beerTranslation = beerDef.LabelCap;

					stringBuilder.AppendLine("ContainsBeer".Translate(beerTranslation));
				}
				else
				{
					stringBuilder.AppendLine("ContainsWort".Translate(this.wortCount, 25, wortTranslation));
				}
			}
			if (!this.Empty)
			{
				if (this.Fermented)
				{
					stringBuilder.AppendLine("Fermented".Translate());
				}
				else
				{
					stringBuilder.AppendLine("FermentationProgress".Translate(this.Progress.ToStringPercent(), this.EstimatedTicksLeft.ToStringTicksToPeriod(true, false, true, true)));
					if (this.CurrentTempProgressSpeedFactor != 1f)
					{
						stringBuilder.AppendLine("FermentationBarrelOutOfIdealTemperature".Translate(this.CurrentTempProgressSpeedFactor.ToStringPercent()));
					}
				}
			}
			stringBuilder.AppendLine("Temperature".Translate() + ": " + base.AmbientTemperature.ToStringTemperature("F0"));
			stringBuilder.AppendLine("IdealFermentingTemperature".Translate() + ": " + 7f.ToStringTemperature("F0") + " ~ " + comp.Props.maxSafeTemperature.ToStringTemperature("F0"));
			return stringBuilder.ToString().TrimEndNewlines();
		}

		public Thing TakeOutBeer()
		{
			if (!this.Fermented)
			{
				Log.Warning("Tried to get beer but it's not yet fermented.");
				return null;
			}

			var beerDef = GetBeerDefForWort(_containingWortType) ?? ThingDefOf.Beer;

			Thing thing = ThingMaker.MakeThing(beerDef, null);
			thing.stackCount = this.wortCount;

			this.Reset();

			return thing;
		}

		private ThingDef GetBeerDefForWort(ThingDef wortDef)
		{
			if (wortDef == null)
				return null;
			// TODO: Optimize
			var defs = DefDatabase<ThingDef>.AllDefs;
			var drugs = defs.Where(d => d.IsWithinCategory(ThingCategoryDefOf.Drugs)).Where(d => d.CostList != null && d.CostList.Count == 1);
			var beer = drugs.SingleOrDefault(d => d.CostList.Count(cl => cl.thingDef.defName.Equals(wortDef.defName)) == 1);

			return beer;
		}

		public override void Draw()
		{
			base.Draw();
			if (!this.Empty)
			{
				Vector3 drawPos = this.DrawPos;
				drawPos.y += 0.04054054f;
				drawPos.z += 0.25f;
				GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
				{
					center = drawPos,
					size = Building_FermentingBarrel.BarSize,
					fillPercent = (float)this.wortCount / 25f,
					filledMat = this.BarFilledMat,
					unfilledMat = Building_FermentingBarrel.BarUnfilledMat,
					margin = 0.1f,
					rotation = Rot4.North
				});
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			IEnumerator<Gizmo> enumerator = null;
			if (Prefs.DevMode && !this.Empty)
			{
				yield return new Command_Action
				{
					defaultLabel = "Debug: Set progress to 1",
					action = delegate ()
					{
						this.Progress = 1f;
					}
				};
			}
			yield break;
		}

		private int wortCount;
		private float progressInt;
		private Material barFilledCachedMat;
		public const int MaxCapacity = 25;
		private const int BaseFermentationDuration = 360000;
		public const float MinIdealTemperature = 7f;
		private static readonly Vector2 BarSize = new Vector2(0.55f, 0.1f);
		private static readonly Color BarZeroProgressColor = new Color(0.4f, 0.27f, 0.22f);
		private static readonly Color BarFermentedColor = new Color(0.9f, 0.85f, 0.2f);
		private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f), false);
	}
}
