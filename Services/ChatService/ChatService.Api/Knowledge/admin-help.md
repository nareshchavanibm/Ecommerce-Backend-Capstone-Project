# Order management

## How do I find or look at an order?
Open **Orders** in the admin area. The list shows the order number, status, date and total, and updates when orders change. Select an order to open its details: customer ID, items, total and current status. In chat you can ask for an order by number (ORD-XXXXXXXX) or ask for recent orders.

## How do I change an order's status?
Open the order's details page in **Orders** and pick a new status from the status dropdown. The statuses are Pending, Confirmed, Processing, Shipped, Delivered and Cancelled. A cancelled order cannot be reopened, and cancelling returns the held stock to inventory. This assistant cannot change statuses; do it on the details page.

# Products

## How do I add a product?
Open **Products** and choose add. Required: name, description, price greater than zero, category and initial stock. The image URL is optional.

## How do I edit or delete a product?
In **Products**, use Edit or Delete on the product row. Edit changes name, description, price, category and image URL. Stock is changed in Inventory. Delete asks for confirmation.

## How do I import products from a file?
Open **Products**, then import. Upload a CSV or XLSX file up to 10 MB with a header row. Name and Price are required. Optional columns: Description, CategoryName, CategoryId, ImageUrl, StockQuantity (or Stock, InitialStock) and ReorderLevel. If stock is missing it defaults to 10 and the reorder level to 2. A CSV template can be downloaded on the import page. The result shows rows imported and any row errors.

## How do I update many products at once?
Open **Products**, then bulk update. Upload a CSV or JSON file with up to 1,000 rows. Each row needs an Id; Name, Description, Price, CategoryId, CategoryName and ImageUrl are optional. Empty cells leave the existing value unchanged. In JSON, use an array of objects such as `{"id": 1, "price": 99.99}`. The result lists how many were updated and any product ids that were not found.

# Categories

## How do I manage categories?
Open **Categories** to see each category with its product count. Add a category with its name, edit the name, or delete with confirmation.

# Inventory

## How do I change stock levels?
Open **Inventory** to see quantity, reorder level and low-stock status per product. Create a record if a product has none, or update the quantity and reorder level. Both must be whole numbers of zero or more.

# Dashboard

## What does the dashboard show?
The admin home shows total orders, revenue from non-cancelled orders, pending orders, revenue over time, top products, a category breakdown and recent activity such as low stock and the latest orders.

# Limits

## What can this assistant not do?
It is read-only. It cannot add or change products, categories, stock or order statuses, and it does not show customer personal details. Use the admin pages for changes.
