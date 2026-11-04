# Boutique Diayma

## Prerequisites

- .NET 10 SDK

## Run the application

From the project directory:

```powershell
dotnet run
```

Application : boutique en ligne ASP.NET Core MVC (`P2FixAnAppDotNetCode`).

Flux métier : liste des produits (`Product`) → panier (`Cart`/`CartLine`) →
commande (`Order`). Route par défaut : `{controller=Product}/{action=Index}/{id?}`
(`Startup.Configure`, `P2FixAnAppDotNetCode/Startup.cs:73-75`).

---

## 6. Exploration de l'application — 2 bugs trouvés

Code exploré : `P2FixAnAppDotNetCode/Program.cs`, `Startup.cs`,
`Controllers/{Product,Cart,Order,Language}Controller.cs`,
`Models/{Cart,Product,Order}.cs`, `Models/Services/{Product,Order}Service.cs`,
`Models/Repositories/{Product,Order}Repository.cs`,
`Components/{CartSummary,LanguageSelector}ViewComponent.cs` et les vues `Views/`.

### Bug 1 — Critique : total et moyenne du panier faux, quantité ignorée

**Fichier :** `P2FixAnAppDotNetCode/Models/Cart.cs:62-80`

```csharp
public double GetTotalValue()
{
    return GetCartLineList().Sum(x => x.Product.Price);          // ❌ sans Quantity
}

public double GetAverageValue()
{
    return GetCartLineList().Average(x => x.Product.Price);      // ❌ sans Quantity
}
```

**Constat :** `GetTotalValue()` et `GetAverageValue()` somment uniquement
`Product.Price`. La quantité (`CartLine.Quantity`) est ignorée.

Exemple : 3 × un article à 10 000 FCFA → le panier affiche 10 000 au lieu de
30 000.

**Incohérence visible :** la vue `Views/Cart/Index.cshtml:21` calcule
correctement le sous-total par ligne :

```cshtml
@((line.Quantity * line.Product.Price).ToString("c"))
```

mais le total final (`Index.cshtml:39`, `@Model.GetTotalValue()`) et la moyenne
(`Index.cshtml:45`) sont faux. Le même total faux est réutilisé dans
`Views/Shared/Components/CartSummary/Default.cshtml:11`.

**Impact :** erreur financière directe, le client sous-paie dès que
`Quantity > 1`.

**Correction attendue :**

```csharp
public double GetTotalValue() =>
    GetCartLineList().Sum(x => x.Product.Price * x.Quantity);

public double GetAverageValue()
{
    var list = GetCartLineList();
    if (list.Count == 0) return 0.0;
    return list.Sum(x => x.Product.Price * x.Quantity) / list.Count;
    // ou / quantité totale selon le sens métier choisi
}
```

### Bug 2 — Critique : panier `Singleton`, partagé entre tous les utilisateurs

**Fichier :** `P2FixAnAppDotNetCode/Startup.cs:29`

```csharp
services.AddSingleton<ICart, Cart>();
```

**Constat :** une seule instance de `Cart` existe pour **toute l'application**.
Tous les visiteurs partagent le même panier : Alice ajoute un produit, Bob le
voit, peut le supprimer ou commander à sa place. Fuite de données et commandes
fantômes entre utilisateurs concurrents.

Facteur aggravant : `IProductRepository` et `IOrderRepository` sont en
`Transient` (`Startup.cs:32-34`). À chaque injection, un nouveau
`ProductRepository` est créé et `GenerateProductData()` (`ProductRepository.cs:22-35`)
recrée le catalogue. Le stock n'est donc pas stable d'une requête à l'autre,
alors que le panier, lui, est global.

**Impact :** inutilisable en production multi-utilisateurs.

**Correction attendue (principe) :**

```csharp
services.AddScoped<ICart, Cart>(); // au minimum : panier par requête/utilisateur
// Idéal : panier stocké en session par utilisateur
services.AddSingleton<IProductRepository, ProductRepository>(); // stock cohérent
services.AddSingleton<IOrderRepository, OrderRepository>();
```

> Autres défauts repérés au passage (non comptés dans les 2 bugs) :
> `CartController.RemoveFromCart` (`CartController.cs:40`) sans `[HttpPost]`
> alors que la vue envoie un `POST`, et `Cart.FindProductInCartLines`
> (`Cart.cs:89`, `.FirstOrDefault().Product`) qui lève une
> `NullReferenceException` si le produit est absent.

---

## 7. Points d'arrêt (breakpoints)

Dans Visual Studio : clic dans la **marge gauche** (gutter) à côté du numéro de
ligne, ou `F9`. Dans VS Code : clic dans la marge, ou `F9` avec le débogueur
attaché, puis `dotnet run` / `F5`. Les points d'arrêt ne modifient **pas** le
code source : ne pas ajouter de `Debugger.Break()` en production.

| # | Fichier | Ligne | Code à l'arrêt | Quand il est atteint |
|---|---------|-------|----------------|----------------------|
| a | `Components/CartSummaryViewComponent.cs` | 12 | `_cart = cart as Cart;` | À chaque rendu de `_Layout.cshtml` (`@await Component.InvokeAsync("CartSummary")`, `_Layout.cshtml:22`) — donc **sur la page d'accueil aussi** |
| b | `Controllers/ProductController.cs` | 15 | `_productService = productService;` | Instanciation de `ProductController` pour `GET /` (route par défaut) — **sur la page d'accueil** |
| c | `Controllers/OrderController.cs` | 17 | `_cart = pCart;` | Instanciation de `OrderController` seulement — **non atteint sur l'accueil**, atteint en naviguant vers `/Order` (checkout) |
| d | `Controllers/CartController.cs` | 15 | `_cart = pCart;` | Instanciation de `CartController` seulement — **non atteint sur l'accueil**, atteint en naviguant vers `/Cart` |
| e | `Startup.cs` | 20 | `Configuration = configuration;` | Dès le démarrage, dans le constructeur `Startup(IConfiguration)` appelé par `Program.Main` — **avant** l'écoute HTTP |

Détail par point d'arrêt :

- **a) `CartSummaryViewComponent.cs:12`** — constructeur
  `CartSummaryViewComponent(ICart cart)`. À inspecter : `cart` (instance
  injectée, `Singleton` — cf. Bug 2, donc la même pour tous les utilisateurs),
  `_cart` après affectation, `((Cart)cart).Lines` et `GetTotalValue()` (Bug 1).
- **b) `ProductController.cs:15`** — constructeur
  `ProductController(IProductService, ILanguageService)`. À inspecter :
  `productService` (type `ProductService`, créé par le DI), `languageService`.
  Confirme que le contrôleur de la route par défaut est bien le produit.
- **c) `OrderController.cs:17`** — constructeur
  `OrderController(ICart, IOrderService, IStringLocalizer<OrderController>)`.
  Point d'entrée du flux de commande : vérifier `_cart` (vide ou non — si vide,
  `OrderController.Index` ajoutera l'erreur `CartEmpty`).
- **d) `CartController.cs:15`** — constructeur
  `CartController(ICart, IProductService)`. Point d'entrée du flux panier :
  vérifier `_cart.Lines` avant `AddToCart` / `RemoveFromCart`.
- **e) `Startup.cs:20`** — constructeur `Startup(IConfiguration configuration)`.
  Premier code utilisateur exécuté. À inspecter : `configuration`
  (`appsettings.json`), puis suivre `ConfigureServices` pour les enregistrements
  DI (`ICart`, `IProductService`, repositories, localisation, session).

> Astuce : démarrer avec les 5 points actifs (`F5`), puis naviguer dans le
> navigateur : `e` (démarrage) → `b` (accueil) → `a` (layout) ; `c` et `d`
> resteront rouges/non atteints tant qu'on ne va pas sur `/Order` et `/Cart`.

---

## 8. Namespaces, classes et méthodes visités avant l'affichage des produits (accueil)

### 8.1 Mode de débogage approprié : « Pas à pas principal » (Step Over, `F10`)

**Choix retenu comme mode principal : Pas à pas principal (Step Over, `F10`).**

Justification :

- L'essentiel du parcours passe par des appels du framework ASP.NET Core
  (`WebApplication.CreateBuilder`, `builder.Build()`, `app.Run()`,
  `UseStaticFiles`, `UseRouting`, `UseEndpoints`, dispatch du routeur vers le
  contrôleur, moteur Razor). En **« Pas à pas détaillé » (Step Into, `F11`)**,
  on entrerait dans des centaines de méthodes internes du framework et on
  perdrait le fil du code de l'application.
- En **« Pas à pas principal » (Step Over, `F10`)**, une méthode appelée est
  exécutée d'un bloc, sans y entrer : on ne visite que les
  namespaces/classes/méthodes **de l'application**, dans l'ordre, ce qui est
  exactement la question posée.
- Le **« Pas à pas sortant » (Step Out, `Maj+F11`)** est un mode d'appoint :
  une fois entré ponctuellement dans une méthode métier (par exemple
  `ProductService.GetAllProducts()` ou `ProductRepository.GetAllProducts()`),
  il permet d'en sortir rapidement et de revenir à l'appelant. Il ne sert pas
  de mode principal de découverte.
- Complément ponctuel : un **« Pas à pas détaillé » très bref** peut être
  nécessaire au point de dispatch du framework pour entrer dans notre
  contrôleur, mais les points d'arrêt de la section 7 rendent ce détour
  inutile : placez les breakpoints `e` → `b` → `a` et laissez `F10` avancer.

En résumé : **principal = Pas à pas principal (`F10`)**, détaillé seulement à
la main pour entrer dans notre code, sortant pour quitter vite une méthode
métier.

### 8.2 Trace chronologique (ordre d'exécution réel)

#### Phase 1 — Démarrage du serveur (avant toute requête HTTP)

| # | Namespace | Classe | Méthode / constructeur | Fichier:ligne |
|---|-----------|--------|------------------------|---------------|
| 1 | `P2FixAnAppDotNetCode` | `Program` | `Main(string[] args)` | `Program.cs:8` |
| 2 | `Microsoft.AspNetCore.Builder` *(framework, franchi en Step Over)* | `WebApplication` | `CreateBuilder(args)` | `Program.cs:10` |
| 3 | `P2FixAnAppDotNetCode` | `Startup` | `Startup(IConfiguration)` — **point d'arrêt e, ligne 20** | `Startup.cs:18` |
| 4 | `P2FixAnAppDotNetCode` | `Startup` | `ConfigureServices(IServiceCollection)` — enregistre `ICart`, `IProductService`, `IProductRepository`, `IOrderService`, `IOrderRepository`, localisation, session | `Startup.cs:26` |
| 5 | `Microsoft.AspNetCore.Builder` *(framework)* | `WebApplication` | `Build()` | `Program.cs:15` |
| 6 | `P2FixAnAppDotNetCode` | `Startup` | `Configure(IApplicationBuilder)` — `UseStaticFiles`, `UseRequestLocalization`, `UseRouting`, `UseSession`, `UseEndpoints` + route par défaut `{controller=Product}/{action=Index}/{id?}` | `Startup.cs:64` |
| 7 | `Microsoft.AspNetCore.Builder` *(framework)* | `WebApplication` | `Run()` — bloque en attente de requêtes | `Program.cs:17` |

Constructeurs créés par le conteneur DI lors de `ConfigureServices` (mais
instanciés seulement à la première requête) :

- `P2FixAnAppDotNetCode.Models.Services` / `ProductService` /
  `ProductService(IProductRepository, IOrderRepository)` (`ProductService.cs:15`)
- `P2FixAnAppDotNetCode.Models.Repositories` / `ProductRepository` /
  `ProductRepository()` → `GenerateProductData()` crée les 9 produits
  (`ProductRepository.cs:13,22`)
- `P2FixAnAppDotNetCode.Models.Repositories` / `OrderRepository` /
  `OrderRepository()` (`OrderRepository.cs:12`)
- `P2FixAnAppDotNetCode.Models.Services` / `OrderService` /
  `OrderService(...)` (`OrderService.cs:15`)
- `P2FixAnAppDotNetCode.Components` / `CartSummaryViewComponent(ICart)`
  (**point d'arrêt a**) et `LanguageSelectorViewComponent` — créés à la
  première requête, pas au démarrage.

#### Phase 2 — Requête `GET /` → routage → contrôleur produit

| # | Namespace | Classe | Méthode / constructeur | Fichier:ligne |
|---|-----------|--------|------------------------|---------------|
| 8 | `Microsoft.AspNetCore.Mvc` *(framework)* | routeur / dispatcher | sélection de `ProductController` via la route par défaut (`Startup.cs:73-75`) | — |
| 9 | `P2FixAnAppDotNetCode.Controllers` | `ProductController` | `ProductController(IProductService, ILanguageService)` — **point d'arrêt b, ligne 15** (`_productService = productService;`) | `ProductController.cs:13` |
| 10 | `P2FixAnAppDotNetCode.Controllers` | `ProductController` | `Index()` | `ProductController.cs:19-23` |

`CartController` (**point d'arrêt d**) et `OrderController` (**point d'arrêt c**)
ne sont **pas** instanciés à ce stade : ils le seront seulement pour `/Cart`
et `/Order`.

#### Phase 3 — Récupération des produits (service + repository)

| # | Namespace | Classe | Méthode | Fichier:ligne |
|---|-----------|--------|---------|---------------|
| 11 | `P2FixAnAppDotNetCode.Models.Services` | `ProductService` | `GetAllProducts()` | `ProductService.cs:24-30` |
| 12 | `P2FixAnAppDotNetCode.Models.Repositories` | `ProductRepository` | `GetAllProducts()` — filtre `Stock > 0`, `OrderBy(p => p.Name)`, retourne `Product[]` | `ProductRepository.cs:41-45` |
| 13 | `P2FixAnAppDotNetCode.Models` | `Product` | propriétés `Id`, `Name`, `Description`, `Stock`, `Price` (déjà créés par `GenerateProductData`) | `Product.cs:14-24` |

`ProductController.Index` fait ensuite `return View(products);`
(`ProductController.cs:22`) : le framework choisit
`Views/Product/Index.cshtml`.

#### Phase 4 — Rendu Razor : vue, layout et composants

| # | Namespace / zone | Classe / fichier | Méthode / rôle | Fichier:ligne |
|---|------------------|------------------|----------------|---------------|
| 14 | `P2FixAnAppDotNetCode` *(Razor)* | `Views/_ViewImports.cshtml` | injecte `IViewLocalizer Localizer`, `TagHelpers` | `_ViewImports.cshtml:1-4` |
| 15 | Razor | `Views/_ViewStart.cshtml` | `Layout = "_Layout"` | `_ViewStart.cshtml:2` |
| 16 | Razor | `Views/Product/Index.cshtml` | boucle `@foreach (Product p in Model)`, affiche `Name`, `Description`, `Price`, `Stock`, formulaire `AddToCart` | `Product/Index.cshtml:27-43` |
| 17 | Razor | `Views/Shared/_Layout.cshtml` | `@await Component.InvokeAsync("CartSummary")` (ligne 22) et `@await Component.InvokeAsync("LanguageSelector")` (ligne 33), `@RenderBody()` | `_Layout.cshtml:22,33` |
| 18 | `P2FixAnAppDotNetCode.Components` | `CartSummaryViewComponent` | constructeur — **point d'arrêt a, ligne 12** (`_cart = cart as Cart;`), puis `Invoke()` → `View(_cart)` | `CartSummaryViewComponent.cs:10,15` |
| 19 | Razor | `Views/Shared/Components/CartSummary/Default.cshtml` | affiche `Lines.Count()`, quantité totale et `@Model.GetTotalValue()` (Bug 1 visible ici) | `CartSummary/Default.cshtml:6,11` |
| 20 | `P2FixAnAppDotNetCode.Components` | `LanguageSelectorViewComponent` | `Invoke(ILanguageService)` → `View(languageService)` | `LanguageSelectorViewComponent.cs:8` |
| 21 | Razor | `Views/Shared/Components/LanguageSelector/Default.cshtml` | formulaire de choix de langue (`ChangeUiLanguage`, `LanguageController`) | `LanguageSelector/Default.cshtml:3` |

À ce stade, le HTML est généré et envoyé au navigateur : la liste des produits
est affichée à l'écran. `OrderController.Index` / `CartController.Index`
(**points d'arrêt c et d**) ne seront visités qu'après un clic sur
« Checkout » (`/Order`) ou sur l'icône panier (`/Cart`).

### 8.3 Synthèse des namespaces visités (accueil)

- `P2FixAnAppDotNetCode` — `Program`, `Startup`
- `P2FixAnAppDotNetCode.Controllers` — `ProductController` (seulement)
- `P2FixAnAppDotNetCode.Models.Services` — `ProductService`
- `P2FixAnAppDotNetCode.Models.Repositories` — `ProductRepository`
- `P2FixAnAppDotNetCode.Models` — `Product`
- `P2FixAnAppDotNetCode.Components` — `CartSummaryViewComponent`,
  `LanguageSelectorViewComponent`
- `Microsoft.AspNetCore.*` — framework (franchi en Pas à pas principal, non
  détaillé)
- `Microsoft.AspNetCore.Mvc.Localization` — `IViewLocalizer` dans les vues

Namespaces **non** visités sur l'accueil : `CartController`, `OrderController`,
`LanguageController` (et leurs services `OrderService`, `LanguageService` en
mode d'action `ChangeUiLanguage`), `Order`, `Cart` en action — ils appartiennent
au flux panier/commande, pas à l'affichage des produits.

### 8.4 Mécanisme de récupération et d'affichage des produits

Dans `ProductController.Index()`, le service récupère les produits, puis la
liste est transmise à la vue :

```csharp
public IActionResult Index()
{
    List<Product> products = _productService.GetAllProducts();
    return View(products);
}
```

`ProductService.GetAllProducts()` délègue au repository et convertit le
résultat en liste avec `ToList()` :

```csharp
public List<Product> GetAllProducts()
{
    return _productRepository.GetAllProducts().ToList();
}
```

La vue `Views/Product/Index.cshtml` utilise cette liste pour générer le HTML
des produits affiché dans le navigateur.

Pendant le rendu, `CartSummaryViewComponent` est aussi appelé depuis
`Views/Shared/_Layout.cshtml` avec
`@await Component.InvokeAsync("CartSummary")`. Son constructeur reçoit le
panier par injection de dépendances et `Invoke()` le transmet à sa vue. Ce
composant affiche le résumé du panier ; il ne récupère pas directement les
produits du catalogue.

### 8.5 Conclusion

Le parcours de débogage relie le point d'entrée `Program.Main()` à la
configuration de `Startup`, puis à `ProductController.Index()` et
`ProductService.GetAllProducts()`. Les produits obtenus sont transmis à la
vue pour affichage. `F10` permet de suivre le code de l'application sans
explorer le framework, tandis que `F11` et `Maj+F11` servent ponctuellement à
entrer dans une méthode métier ou à en sortir.
