#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2014 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */
#endregion

#region Using Statements
using System;
#endregion

namespace Microsoft.Xna.Framework.Content
{
	/* http://msdn.microsoft.com/en-us/library/microsoft.xna.framework.content.contentserializerattribute.aspx
	 * The class definition on msdn site shows: [AttributeUsageAttribute(384)]
	 * The following code var ff = (AttributeTargets)384; shows that ff is Field | Property
	 * so that is what we use.
	 */
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public sealed class ContentSerializerAttribute : Attribute
	{
		#region Public Properties

		public bool AllowNull
		{
			get
			{
				return this.allowNull;
			}
			set
			{
				this.allowNull = value;
			}
		}

		public string CollectionItemName
		{
			get
			{
				return this.collectionItemName;
			}
			set
			{
				this.collectionItemName = value;
			}
		}

		public string ElementName
		{
			get
			{
				return this.elementName;
			}
			set
			{
				this.elementName = value;
			}
		}

		public bool FlattenContent
		{
			get
			{
				return this.flattenContent;
			}
			set
			{
				this.flattenContent = value;
			}
		}

		public bool HasCollectionItemName
		{
			get
			{
				return this.hasCollectionItemName;
			}
		}

		public bool Optional
		{
			get
			{
				return this.optional;
			}
			set
			{
				this.optional = value;
			}
		}

		public bool SharedResource
		{
			get{
				return this.sharedResource;
			}
			set{
				this.sharedResource = value;
			}
		}

		#endregion

		#region Private Variables

		private bool allowNull;
		private string collectionItemName;
		private string elementName;
		private bool flattenContent;
		private bool hasCollectionItemName;
		private bool optional;
		private bool sharedResource;

		#endregion

		#region Public Constructor

		public ContentSerializerAttribute()
		{
		}

		#endregion

		#region Public Methods

		public ContentSerializerAttribute Clone()
		{
			ContentSerializerAttribute clone = new ContentSerializerAttribute();
			clone.allowNull = this.allowNull;
			clone.collectionItemName = this.collectionItemName;
			clone.elementName = this.elementName;
			clone.flattenContent = this.flattenContent;
			clone.hasCollectionItemName = this.hasCollectionItemName;
			clone.optional = this.optional;
			clone.sharedResource = this.sharedResource;
			return clone;
		}

		#endregion
	}
}
