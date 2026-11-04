using System.Collections.Generic;
using System.Linq;

namespace P2FixAnAppDotNetCode.Models.Repositories
{
    /// <summary>
    /// The class that manages product data
    /// </summary>
    public class ProductRepository : IProductRepository
    {
        private static List<Product> _products;

        public ProductRepository()
        {
            _products = new List<Product>();
            GenerateProductData();
        }

        /// <summary>
        /// Generate the default list of products
        /// </summary>
        private void GenerateProductData()
        {
            int id = 0;
            _products.Add(new Product(++id, 10, 88000, "XIAOMI Redmi 13C ", "256GB ROM/8GB RAM GSM 6.74"));
            _products.Add(new Product(++id, 20, 30000, "Micro-ondes 20 Litres ", "- 20MAF3-IA - Gris"));
            _products.Add(new Product(++id, 30, 7000, "Music Pioneer Bluetooth ", "Casque Bluetooth Sans Fil P9"));
            _products.Add(new Product(++id, 40, 19000, "Disque dur externe mobile USB3.0 500GB", "USB3.0 500GB"));
            _products.Add(new Product(++id, 50, 5000, "Multiprise Rallonge - 4 Prises-", " longueur 10 mètres - bleu "));
            _products.Add(new Product(++id, 10, 130000, "Galaxy A25 5G RAM 6Go  ", "- ROM 128Go - 5000 mAh - Black"));
            _products.Add(new Product(++id, 20, 1000, "Clé USB - 64GB + Porte-clés - Argent ", "64GB + Porte-clés - Argent"));
            _products.Add(new Product(++id, 30, 11500, "Cuisinière à Gaz 4 Feux – ", "RGCN-50- SB– 50x50 cm – 100% Inox"));
            _products.Add(new Product(++id, 40, 35000, "Fontaine à Eau 2 Robinets Chaud/normal", " - Bdt-hn567 - Blanc"));

            }


        /// <summary>
        /// Get all products from the inventory
        /// </summary>
        public Product[] GetAllProducts()
        {
            List<Product> list = _products.Where(p => p.Stock > 0).OrderBy(p => p.Name).ToList();
            return list.ToArray();
        }

        /// <summary>
        /// Update the stock of a product in the inventory by its id
        /// </summary>
        public void UpdateProductStocks(int productId, int quantityToRemove)
        {
            Product product = _products.First(p => p.Id == productId);
            product.Stock = product.Stock - quantityToRemove;

            if (product.Stock == 0)
                _products.Remove(product);
        }
    }
}
