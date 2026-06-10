/*
 * Garmetix
 * Author: Amit Kumar
 * https://garmetix.com/
 * Copyright (c) 2026. All rights reserved.
 * Version: 6.0.0
 * License: https://garmetix.com/license
 * Website: https://garmetix.com/
*/
/*
 * Inventory.cs
 * 
 * This file contains the definitions of classes related to inventory management in the Garmetix application.
 * It includes classes for Units of Measurement (UOM), Taxes, Stock, Products, Product Categories, and Product Details.
 * These classes are designed to represent the various entities and their relationships within the inventory system.
 * 
 * The classes are structured to support features such as tracking stock levels, calculating tax rates, and categorizing products.
 * They also include properties for handling product details like barcodes, descriptions, and pricing information.
 * 
 * The use of attributes like [JsonIgnore] helps to control the serialization of certain properties when converting objects to JSON format.
 * This is particularly useful for properties that are calculated or derived from other properties, ensuring that only relevant data is included in API responses or data storage.
 */

using Garmetix.Core.Enums;
using Enums = Garmetix.Core.Enums;
using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;


namespace Garmetix.Core.Models.Inventory
{


    /// <summary>    
    ///     The UOM class represents a Unit of Measurement in the inventory system. It includes properties for the name of the unit, whether it allows decimal values, and the number of decimal places.
    ///    Will be used in Product and Stock classes to define the unit of measurement for products and stock items.
    ///    //TODO: When Fabric is implemented, UOM will be used to define the unit of measurement for fabrics (e.g., meters, yards) and for finished products (e.g., pieces).
    ///    </summary>
    public class UOM : CompanyBase
    {

        [Display(Name = "Name")] public required string Name { get; set; }
        [Display(Name = "Decimal")] public bool Decimal { get; set; } = false;
        [Display(Name = "Decimal Place")] public int DecimalPlace { get; set; } = 0;
    }

    public class Tax : BaseEntity
    {
        [Display(Name = "Name")] public required string Name { get; set; }
        [Display(Name = "Composite Rate")] public decimal CompositeRate { get; set; }
        [Display(Name = "Tax Type")] public TaxType TaxType { get; set; } = TaxType.GST;

        [JsonIgnore]
        [Display(Name = "IGST")] public decimal IGST { get => TaxType == TaxType.IGST ? CompositeRate : 0; }
        [JsonIgnore]
        [Display(Name = "CGST")] public decimal CGST { get => TaxType == TaxType.GST ? CompositeRate / 2m : TaxType == TaxType.CGST ? CompositeRate : 0; }
        [JsonIgnore]
        [Display(Name = "SGST")] public decimal SGST { get => TaxType == TaxType.GST ? CompositeRate / 2m : TaxType == TaxType.SGST ? CompositeRate : 0; }
    }

    public class Stock : StoreBase
    {
        [ForeignKey("Product")]
        [Display(Name = "Product", AutoGenerateField = false)] public Guid ProductId { get; set; }
        [Display(Name = "Barcode")] public required string Barcode { get; set; }
        [Display(Name = "HSN Code")] public string? HSNCode { get; set; }
        [Display(Name = "Unit")] public Unit Unit { get; set; }
        [Display(Name = "Purchase Quantity")] public decimal PurchaseQty { get; set; } = 0;
        [Display(Name = "Cost Price")] public decimal CostPrice { get; set; } = 0;
        [Display(Name = "Sold Quantity")] public decimal SoldQty { get; set; } = 0;
        [Display(Name = "MRP")] public decimal MRP { get; set; } = 0;
        [Display(Name = "Tax Rate")] public decimal TaxRate { get; set; }
        [Display(Name = "Tax Type")] public TaxType TaxType { get; set; }
        [Display(Name = "Tax", AutoGenerateField = false)] public Guid TaxId { get; set; }
        [Display(Name = "Branded Product")] public bool BrandedProduct { get; set; } = true;

        [Display(Name = "Sold Value")] public decimal SoldValue { get; set; } = 0;
        [Display(Name = "Non-GST / Out-of-Scope Stock")] public bool IsOFB { get; set; } = false;
        public StockType StockType { get; set; } = StockType.Billed;

        [JsonIgnore]
        [Display(Name = "Tax", AutoGenerateField = false)] public virtual Tax? Tax { get; set; }
        [JsonIgnore]
        [Display(Name = "Product", AutoGenerateField = false)] public virtual Product? Product { get; set; }

        [JsonIgnore]
        [Display(Name = "Current Stock")] public decimal CurrentStock { get => PurchaseQty - SoldQty; }
        [JsonIgnore]
        [Display(Name = "Basic MRP")] public decimal BasicMRP { get => MRP / (1 + (TaxRate / 100)); }
        [JsonIgnore]
        [Display(Name = "Unit Tax")] public decimal UnitTax { get => MRP - BasicMRP; }
        [JsonIgnore]
        [Display(Name = "Cost Value")] public decimal CostValue { get => CostPrice * CurrentStock; }
        [JsonIgnore]
        [Display(Name = "MRP Value")] public decimal MRPValue { get => MRP * CurrentStock; }
    }

    public class Product : GroupBase
    {
        [Display(Name = "Product Name")] public required string Name { get; set; }
        [Display(Name = "Barcode")] public required string Barcode { get; set; }
        [Display(Name = "Descriptions")] public string? Descriptions { get; set; }
        [Display(Name = "MRP")] public decimal MRP { get; set; }
        [Display(Name = "Tax Rate")] public decimal TaxRate { get; set; }
        [Display(Name = "HSN")] public string? HSNCode { get; set; } = string.Empty;
        //TODO: Need to use Basic Rate Calucator Static Function need to be create in toolkit
        [Display(Name = "Basic Rate", AutoGenerateField = false)] public decimal BasicPrice => MRP / (1 + (TaxRate / 100));
        [Display(Name = "Unit")] public Unit Unit { get; set; }
        [Display(Name = "Tax Type")] public TaxType TaxType { get; set; }
        [Display(Name = "Product Type")] public ProductType ProductType { get; set; } = ProductType.Fabric;
        [Display(Name = "Product Group")] public ProductGroup ProductGroup { get; set; } = Enums.ProductGroup.Shirting;
        [Display(Name = "Product Category", AutoGenerateField = false)] public Guid ProductCategoryId { get; set; }
        [Display(Name = "Product Sub Category", AutoGenerateField = false)] public Guid ProductSubCategoryId { get; set; }
        [Display(Name = "Product Category", AutoGenerateField = false)] public virtual ProductCategory? ProductCategory { get; set; }
        [Display(Name = "Product Sub Category", AutoGenerateField = false)] public virtual ProductSubCategory? ProductSubCategory { get; set; }
        [Display(Name = "Stocks", AutoGenerateField = false)] public virtual ICollection<Stock>? Stocks { get; set; } = null;

        // NEW: Add this so the SfAutocomplete has a property to bind to
        [NotMapped]
        public string DisplayText => $"{Name} ({Barcode})";

    }



    public class StockMovement : StoreBase
    {
        [Display(Name = "Stock", AutoGenerateField = false)] public Guid? StockId { get; set; }
        [Display(Name = "Product", AutoGenerateField = false)] public Guid ProductId { get; set; }
        [Display(Name = "Barcode")] public required string Barcode { get; set; } = string.Empty;
        [Display(Name = "Movement Type")] public required string MovementType { get; set; } = string.Empty;
        [Display(Name = "Quantity In")] public decimal QuantityIn { get; set; }
        [Display(Name = "Quantity Out")] public decimal QuantityOut { get; set; }
        [Display(Name = "Cost Price")] public decimal CostPrice { get; set; }
        [Display(Name = "MRP")] public decimal MRP { get; set; }
        [Display(Name = "Tax Rate")] public decimal TaxRate { get; set; }
        [Display(Name = "HSN Code")] public string? HSNCode { get; set; }
        [Display(Name = "Source Type")] public string? SourceType { get; set; }
        [Display(Name = "Source", AutoGenerateField = false)] public Guid? SourceId { get; set; }
        [Display(Name = "Source Number")] public string? SourceNumber { get; set; }
        [Display(Name = "Remarks")] public string? Remarks { get; set; }
        [Display(Name = "Movement Date")] public DateTime OnDate { get; set; } = DateTime.Now;
        public virtual Product? Product { get; set; }
        public virtual Stock? Stock { get; set; }
    }


    public class NonGstGoodsDocument : StoreBase
    {
        [Display(Name = "Document Number")] public string DocumentNumber { get; set; } = string.Empty;
        [Display(Name = "Date")] public DateTime OnDate { get; set; } = DateTime.Now;
        [Display(Name = "Document Type")] public NonGstGoodsDocumentType DocumentType { get; set; } = NonGstGoodsDocumentType.Sale;
        [Display(Name = "Party Name")] public string PartyName { get; set; } = string.Empty;
        [Display(Name = "Vendor", AutoGenerateField = false)] public Guid? VendorId { get; set; }
        [Display(Name = "Customer", AutoGenerateField = false)] public Guid? CustomerId { get; set; }
        [Display(Name = "Payment Mode")] public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;
        [Display(Name = "Reference Number")] public string? ReferenceNumber { get; set; }
        [Display(Name = "Gross Amount")] public decimal GrossAmount { get; set; }
        [Display(Name = "Discount Amount")] public decimal DiscountAmount { get; set; }
        [Display(Name = "Net Amount")] public decimal NetAmount { get; set; }
        [Display(Name = "Ledger", AutoGenerateField = false)] public Guid? LedgerId { get; set; }
        [Display(Name = "Remarks")] public string? Remarks { get; set; }
        [JsonIgnore] public virtual ICollection<NonGstGoodsItem>? Items { get; set; }
    }

    public class NonGstGoodsItem : CompanyBase
    {
        [Display(Name = "Document", AutoGenerateField = false)] public Guid DocumentId { get; set; }
        [Display(Name = "Product", AutoGenerateField = false)] public Guid ProductId { get; set; }
        [Display(Name = "Stock", AutoGenerateField = false)] public Guid? StockId { get; set; }
        [Display(Name = "Barcode")] public string Barcode { get; set; } = string.Empty;
        [Display(Name = "Product Name")] public string ProductName { get; set; } = string.Empty;
        [Display(Name = "Quantity")] public decimal Quantity { get; set; }
        [Display(Name = "Rate")] public decimal Rate { get; set; }
        [Display(Name = "Discount Amount")] public decimal DiscountAmount { get; set; }
        [Display(Name = "Amount")] public decimal Amount { get; set; }
        [JsonIgnore] public virtual NonGstGoodsDocument? Document { get; set; }
        [JsonIgnore] public virtual Product? Product { get; set; }
        [JsonIgnore] public virtual Stock? Stock { get; set; }
    }

    public class ProductCategory : CompanyBase
    {
        [Display(Name = "Category Name")] public string Name { get; set; } = string.Empty;
        public ProductGroup? ProductGroup { get; set; } = Enums.ProductGroup.Shirting;
        public bool IsActive { get; set; } = true;

        [NotMapped]
        public virtual ICollection<ProductSubCategory>? SubCategories { get; set; }
    }

    public class ProductSubCategory : CompanyBase
    {
        [Display(Name = "Sub Category Name")] public string Name { get; set; } = string.Empty;
        public Guid? CategoryId { get; set; }

        [NotMapped]
        public virtual ProductCategory? Category { get; set; }
    }

    public class ProductDetail : CompanyBase
    {
        [Display(Name = "Product", AutoGenerateField = false)] public Guid ProductId { get; set; }
        [Display(Name = "Barcode")] public required string Barcode { get; set; }
        [Display(Name = "Style Code")] public string? StyleCode { get; set; }
        [Display(Name = "Base Color")] public string? BaseColor { get; set; }
        [Display(Name = "Brand")] public string? Brand { get; set; }
        [Display(Name = "Vendor", AutoGenerateField = false)] public Guid? VendorId { get; set; }
        [Display(Name = "Vendor", AutoGenerateField = false)] public virtual Vendor? Vendor { get; set; }
        [Display(Name = "Product", AutoGenerateField = false)] public virtual Product? Product { get; set; }
    }

    public class ProductAttribute
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
    }

    public class ProductAttributeValue
    {
        public Guid ProductId { get; set; }
        public Guid AttributeId { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    public class ProductTag
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
    }

    public class ProductTagMapping
    {
        public Guid ProductId { get; set; }
        public Guid TagId { get; set; }
    }

    public class Brand : BaseEntity
    {
        [Display(Name = "Brand Name")] public string Name { get; set; } = string.Empty;
        [Display(Name = "Brand Code")] public string BrandCode { get; set; } = string.Empty;
        [Display(Name = "Supplier", AutoGenerateField = false)] public Guid? SupplierId { get; set; } = null;
    }

    public class DocumentSequence : CompanyBase
    {
        [Display(Name = "Document Type")] public string DocumentType { get; set; } = string.Empty;
        [Display(Name = "Prefix")] public string Prefix { get; set; } = string.Empty;
        [Display(Name = "Sequence Date")] public DateTime SequenceDate { get; set; } = DateTime.Today;
        [Display(Name = "Last Number")] public int LastNumber { get; set; } = 0;
        [Display(Name = "Store Group", AutoGenerateField = false)] public Guid? StoreGroupId { get; set; }
        [Display(Name = "Store", AutoGenerateField = false)] public Guid? StoreId { get; set; }
    }

}
