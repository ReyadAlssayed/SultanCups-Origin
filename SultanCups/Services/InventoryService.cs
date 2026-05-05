using SultanCups.Data;
using SultanCups.Models;
using Microsoft.EntityFrameworkCore;

namespace SultanCups.Services
{
    public class InventoryService
    {
        private readonly AppDbContext _context;

        public InventoryService(AppDbContext context)
        {
            _context = context;
        }

        // ================================
        // 🔹 هذا الجزء خاص بجدول products
        // ================================

        public async Task<List<Product>> GetProducts()
        {
            return await _context.products
                .AsNoTracking() // الإضافة هنا
                .Where(p => p.is_active)
                .ToListAsync();
        }

        public async Task AddProduct(Product product)
        {
            _context.products.Add(product);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Product>> GetAllProducts()
        {
            return await _context.products.ToListAsync();
        }

        public async Task<bool> UpdateProduct(Product updatedProduct)
        {
            var product = await _context.products
                .FirstOrDefaultAsync(p => p.product_id == updatedProduct.product_id);

            if (product == null)
                return false;

            product.name = updatedProduct.name;
            product.sale_price = updatedProduct.sale_price; // 🔥 هذا الناقص
            product.is_active = updatedProduct.is_active;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteProduct(int productId)
        {
            var product = await _context.products
                .FirstOrDefaultAsync(p => p.product_id == productId);

            if (product == null)
                return false;

            product.is_active = false;

            await _context.SaveChangesAsync();
            return true;
        }

        // =========================================
        // 🔹 هذا الجزء خاص بجدول production (الإنتاج)
        // =========================================

        public async Task<List<Production>> GetProduction()
        {
            return await _context.production
                .AsNoTracking() // الإضافة هنا
                .Include(p => p.Product)
                .ToListAsync();
        }

        public async Task AddProduction(Production production)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                production.production_date = DateTime.SpecifyKind(
                    production.production_date,
                    DateTimeKind.Unspecified);

                production.notes = production.notes?.Trim();

                // 1. حفظ الإنتاج
                _context.production.Add(production);

                // 2. تحديث المخزون
                var stock = await _context.product_stock
                    .FirstOrDefaultAsync(s => s.product_id == production.product_id);

                if (stock == null)
                {
                    stock = new ProductStock
                    {
                        product_id = production.product_id,
                        quantity = production.box_count
                    };

                    _context.product_stock.Add(stock);
                }
                else
                {
                    stock.quantity += production.box_count;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
           }
        }

        public async Task<string> UpdateProduction(Production updated)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var production = await _context.production
                    .FirstOrDefaultAsync(p => p.production_id == updated.production_id);

                if (production == null)
                    return "not_found";

                var stock = await _context.product_stock
                    .FirstOrDefaultAsync(s => s.product_id == production.product_id);

                if (stock == null)
                    return "stock_not_found";

                // الفرق
                var diff = updated.box_count - production.box_count;
                var newStockQty = stock.quantity + diff;

                // 🔥 منع السالب
                if (newStockQty < 0)
                    return "لا يمكن تعديل الإنتاج: الكمية المصروفة أكبر من المتبقي في المخزون.";

                // التعديل
                stock.quantity = newStockQty;

                production.box_count = updated.box_count;
                production.notes = updated.notes?.Trim();
                production.production_date = DateTime.SpecifyKind(
                    updated.production_date,
                    DateTimeKind.Unspecified);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return "updated";
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<string> DeleteProduction(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var production = await _context.production
                    .FirstOrDefaultAsync(p => p.production_id == id);

                if (production == null)
                    return "not_found";

                var stock = await _context.product_stock
                    .FirstOrDefaultAsync(s => s.product_id == production.product_id);

                if (stock == null)
                    return "stock_not_found";

                // 🔥 تحقق قبل الحذف
                var newStockQty = stock.quantity - production.box_count;

                if (newStockQty < 0)
                    return "لا يمكن حذف الإنتاج: الكمية المصروفة أكبر من المتبقي في المخزون.";

                stock.quantity = newStockQty;

                _context.production.Remove(production);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return "deleted";
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        // =========================================
        // 🔹 هذا الجزء خاص بجدول suppliers (الموردين)
        // =========================================

        // جلب جميع الموردين
        public async Task<List<Supplier>> GetSuppliers()
        {
            return await _context.suppliers
                .AsNoTracking()
                .ToListAsync();
        }

        // إضافة مورد جديد
        public async Task AddSupplier(Supplier supplier)
        {
            _context.suppliers.Add(supplier);
            await _context.SaveChangesAsync();
        }

        // تعديل مورد
        public async Task<bool> UpdateSupplier(Supplier updated)
        {
            var supplier = await _context.suppliers
                .FirstOrDefaultAsync(s => s.supplier_id == updated.supplier_id);

            // لو غير موجود
            if (supplier == null)
                return false;

            // تحديث البيانات
            supplier.name = updated.name;
            supplier.phone = updated.phone;
            supplier.email = updated.email;
            supplier.location = updated.location;
            supplier.is_active = updated.is_active;
            supplier.notes = updated.notes;

            await _context.SaveChangesAsync();
            return true;
        }

        // حذف مورد
        public async Task<string> DeleteOrDisableSupplier(int id)
        {
            var supplier = await _context.suppliers
                .FirstOrDefaultAsync(s => s.supplier_id == id);

            if (supplier == null)
                return "not_found";

            // ✅ الربط الصحيح هنا
            var hasPurchases = await _context.purchases
                .AnyAsync(p => p.supplier_id == id);

            if (hasPurchases)
            {
                supplier.is_active = false;
                await _context.SaveChangesAsync();
                return "disabled";
            }

            _context.suppliers.Remove(supplier);
            await _context.SaveChangesAsync();
            return "deleted";
        }

        public async Task<List<Supplier>> GetAllSuppliers()
        {
            return await _context.suppliers
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Supplier>> GetActiveSuppliers()
        {
            return await _context.suppliers
                .AsNoTracking()
                .Where(x => x.is_active)
                .OrderBy(x => x.name)
                .ToListAsync();
        }

        // =========================================
        // 🔹 raw_materials (المواد الخام)
        // =========================================

        public async Task<List<RawMaterial>> GetActiveRawMaterials()
        {
            return await _context.raw_materials
                .AsNoTracking()
                .Where(r => r.is_active)
                .ToListAsync();
        }

        public async Task<List<RawMaterial>> GetAllRawMaterials()
        {
            return await _context.raw_materials
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task AddRawMaterial(RawMaterial material)
        {
            _context.raw_materials.Add(material);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateRawMaterial(RawMaterial updated)
        {
            var material = await _context.raw_materials
                .FirstOrDefaultAsync(r => r.raw_material_id == updated.raw_material_id);

            if (material == null)
                return false;

            material.name = updated.name;
            material.size = updated.size;
            material.unit_of_measure = updated.unit_of_measure;
            material.unit_cost = updated.unit_cost;
            material.is_active = updated.is_active;
            material.notes = updated.notes;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<string> DeleteOrDisableRawMaterial(int id)
        {
            var material = await _context.raw_materials
                .FirstOrDefaultAsync(r => r.raw_material_id == id);

            if (material == null)
                return "not_found";

            var hasPurchases = await _context.purchases
                .AnyAsync(p => p.raw_material_id == id);

            if (hasPurchases)
            {
                material.is_active = false;
                await _context.SaveChangesAsync();
                return "disabled";
            }

            _context.raw_materials.Remove(material);
            await _context.SaveChangesAsync();

            return "deleted";
        }

        //this is for product strock (view only)
        public async Task<List<ProductStock>> GetStock()
        {
            return await _context.product_stock
                .Include(p => p.Product) // 🔥 هذا المهم
                .AsNoTracking()
                .ToListAsync();
        }
    }
}