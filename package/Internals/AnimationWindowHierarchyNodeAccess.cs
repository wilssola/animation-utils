using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Needle
{
	public static class AnimationWindowHierarchyNodeAccess
	{
		public static bool DrawNodeRow(Rect rect, object nodeObj, object hierarchy)
		{
			if (nodeObj is AnimationWindowHierarchyNode node && hierarchy is AnimationWindowHierarchyGUI gui)
			{
				var isMissing = IsMissing(node);
				var isMissingOrShortcut = isMissing || Event.current.modifiers == EventModifiers.Alt;
				// if the binding is missing and we drag an object
				if (isMissingOrShortcut && DragAndDrop.objectReferences.Length > 0)
				{
					var path = node.path;
					if (path == null) return false;
					var lastPathPartIndex = path.LastIndexOf("/", StringComparison.Ordinal);
					if (lastPathPartIndex > 0)
					{
						// ReSharper disable once ReplaceSubstringWithRangeIndexer
						path = path.Substring(lastPathPartIndex + 1);
					}
					var label = path + " : " + node.displayName;
					var type = node.animatableObjectType;
					var obj = EditorGUI.ObjectField(rect, label, null, type, true);
					var assignedObject = obj;
					if (obj is Component comp) obj = comp.gameObject;
					if (obj && obj is GameObject go)
					{
						var transform = go.transform;
						foreach (var curve in node.curves)
						{
							if (!isMissing)
							{
								var currentlyBoundObject = AnimationUtility.GetAnimatedObject(curve.rootGameObject, curve.binding);
								if (currentlyBoundObject == assignedObject)
								{
									Debug.Log("Already bound to " + assignedObject, assignedObject);
									continue;
								}
							}
							var root = curve.rootGameObject.transform;
							if (!IsChild(root, transform))
							{
								Debug.LogError("Can not assign " + transform.name + " because it's no child of " + root.name, root);
								continue;
							}
							Debug.Log("<b>Update animation target</b> with: " + obj + 
							          "\nPrevious binding: " + node.path + "." + node.propertyName + "; " + node.propertyName, obj);
							Undo.RegisterCompleteObjectUndo(curve.clip, "Replace curve");
							var objPath = AnimationUtility.CalculateTransformPath(transform, root);
							curve.clip.SetCurve(objPath, curve.type, curve.propertyName, curve.ToAnimationCurve());
							RemoveCurve(gui, node);
						}
					}
					return true;
				}
			}
			return false;
		}

		public static bool IsMissing(object obj, out EditorCurveBinding binding)
		{
			if (obj is AnimationWindowHierarchyNode node)
			{
				binding = node.binding.GetValueOrDefault();
				return binding != null && IsMissing(node);
			}
			binding = default;
			return false;
		}

		private static bool IsMissing(AnimationWindowHierarchyNode node)
		{
			return AnimationWindowUtility.IsNodeLeftOverCurve(node);
		}

		private static bool IsChild(Transform root, Transform possibleChild)
		{
			var current = possibleChild;
			while (current)
			{
				if (current == root) return true;
				current = current.parent;
			}
			return false;
		}

		private static void RemoveCurve(AnimationWindowHierarchyGUI gui, AnimationWindowHierarchyNode node)
		{
			var state = gui.state;

			AnimationWindowHierarchyNode hierarchyNode = node;

			if (hierarchyNode.parent is AnimationWindowHierarchyPropertyGroupNode && hierarchyNode.binding != null &&
			    AnimationWindowUtility.ForceGrouping((EditorCurveBinding)hierarchyNode.binding))
				hierarchyNode = (AnimationWindowHierarchyNode)hierarchyNode.parent;

			if (hierarchyNode.curves == null)
				return;

			List<AnimationWindowCurve> curves = null;

			// Property or propertygroup
			if (hierarchyNode is AnimationWindowHierarchyPropertyGroupNode || hierarchyNode is AnimationWindowHierarchyPropertyNode)
				curves = AnimationWindowUtility.FilterCurves(hierarchyNode.curves.ToArray(), hierarchyNode.path, hierarchyNode.animatableObjectType,
					hierarchyNode.propertyName);
			else
				curves = AnimationWindowUtility.FilterCurves(hierarchyNode.curves.ToArray(), hierarchyNode.path, hierarchyNode.animatableObjectType);

			foreach (AnimationWindowCurve animationWindowCurve in curves)
				state.RemoveCurve(animationWindowCurve, "Remove AnimationCurve");
		}
	}
}