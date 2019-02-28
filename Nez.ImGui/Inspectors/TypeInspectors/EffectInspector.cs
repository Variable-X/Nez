﻿using System.Collections.Generic;
using System.Reflection;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez.IEnumerableExtensions;


namespace Nez.ImGuiTools.TypeInspectors
{
	public class EffectInspector : AbstractTypeInspector
	{
		List<AbstractTypeInspector> _inspectors = new List<AbstractTypeInspector>();

		public override void initialize()
		{
			base.initialize();

			// we either have a getter that gets a Material or an Effect
			var effect = getValue<Effect>();
			if( effect == null )
				return;

			_name = effect.GetType().Name;

			var inspectors = TypeInspectorUtils.getInspectableProperties( effect );
			foreach( var inspector in inspectors )
			{
				// we dont need the Name field. It serves no purpose.
				if( inspector.name != "Name" )
					_inspectors.Add( inspector );
			}
		}

		public override void drawMutable()
		{
			var isOpen = ImGui.CollapsingHeader( $"{_name}", ImGuiTreeNodeFlags.FramePadding );
			if( ImGui.BeginPopupContextItem() )
			{
				if( ImGui.Selectable( "Remove Effect" ) )
				{
					setValue( null );
					_isTargetDestroyed = true;
				}

				ImGui.EndPopup();
			}

			if( isOpen && !_isTargetDestroyed )
			{
				foreach( var i in _inspectors )
					i.draw();
			}
		}
	}
}
