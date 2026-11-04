using System.Collections.Generic;
using System.Linq;

namespace P2FixAnAppDotNetCode.Models
{
    /// <summary>
    /// The Cart class
    /// </summary>
    public class Cart : ICart
    {
        
        public IEnumerable<CartLine> Lines => _lignesPanier;
        private List<CartLine> _lignesPanier = new List<CartLine>();

        private List<CartLine> GetCartLineList()
        {
            return _lignesPanier;
        }
        /// <summary>
        /// Adds a product in the cart or increment its quantity in the cart if already added
        /// </summary>//
        public void AddItem(Product product, int quantity)
        {
            // TODO implement the method
            // DONE OD
            CartLine cartLine = null;

            foreach (var line in _lignesPanier)
            {
                if (line.Product.Id == product.Id)
                {
                    cartLine = line;
                    break;
                }
            }

            // Si le produit est déjà dans le panier, augmentez la quantité
            if (cartLine != null)
            {
                cartLine.Quantity += quantity;
            }
            // Sinon, ajoutez un nouvel élément CartLine au panier
            else
            {
                _lignesPanier.Add(new CartLine
                {
                    Product = product,
                    Quantity = quantity
                });
            }
        }

        /// <summary>
        /// Removes a product form the cart
        /// </summary>
        public void RemoveLine(Product product) =>
            GetCartLineList().RemoveAll(l => l.Product.Id == product.Id);

        /// <summary>
        /// Get total value of a cart
        /// </summary>
        public double GetTotalValue()
        {
            // TODO implement the method
            // DONE OD
            return GetCartLineList().Sum(x => x.Product.Price);
        }

        /// <summary>
        /// Get average value of a cart
        /// </summary>
        public double GetAverageValue()
        {
            // TODO implement the method
            // DONE OD
            if (GetCartLineList().Count > 0)
                return GetCartLineList().Average(x => x.Product.Price);
            else
                return 0.0;
        }

        /// <summary>
        /// Looks after a given product in the cart and returns if it finds it
        /// </summary>
        public Product FindProductInCartLines(int productId)
        {
            // TODO implement the method
            // DONE OD
            return GetCartLineList().Where(x => x.Product.Id == productId).FirstOrDefault().Product;
        }

        /// <summary>
        /// Get a specifid cartline by its index
        /// </summary>
        public CartLine GetCartLineByIndex(int index)
        {
            return Lines.ToArray()[index];
        }

        /// <summary>
        /// Clears a the cart of all added products
        /// </summary>
        public void Clear()
        {
            List<CartLine> cartLines = GetCartLineList();
            cartLines.Clear();
        }
    }

    public class CartLine
    {
        public int OrderLineId { get; set; }
        public Product Product { get; set; }
        public int Quantity { get; set; }
    }
}
