using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using NexusCommunication.Data;
using NexusCommunication.Models;

namespace NexusCommunication.Helpers;

/// <summary>
/// Database dependent validation of a new connection order.
/// It is shared by OrdersController (retail staff) and MyPortalController (customers) so both places
/// apply exactly the same rules. The method only ADDS errors to ModelState; it never changes the order.
/// </summary>
public static class OrderValidation
{
    public static async Task ValidateReferencesAsync(ApplicationDbContext db, Order order, ModelStateDictionary modelState)
    {
        // --- Customer -----------------------------------------------------------------------------------------
        if (order.CustomerId > 0 && !await db.Customers.AnyAsync(c => c.Id == order.CustomerId))
            modelState.AddModelError(nameof(Order.CustomerId), "The selected customer does not exist.");

        // --- City and retail store ----------------------------------------------------------------------------
        if (order.CityId > 0 && !await db.Cities.AnyAsync(c => c.Id == order.CityId))
            modelState.AddModelError(nameof(Order.CityId), "The selected city does not exist.");

        if (order.RetailStoreId > 0)
        {
            var store = await db.RetailStores.FirstOrDefaultAsync(s => s.Id == order.RetailStoreId);
            if (store == null)
                modelState.AddModelError(nameof(Order.RetailStoreId), "The selected retail store does not exist.");
            else if (!store.IsActive)
                modelState.AddModelError(nameof(Order.RetailStoreId), "The selected retail store is not active.");
            else if (order.CityId > 0 && store.CityId != order.CityId)
                modelState.AddModelError(nameof(Order.RetailStoreId), "The selected retail store is not in the selected city.");
        }

        // --- Main plan: must exist, be active and match the connection type -----------------------------------
        if (order.PlanId > 0)
        {
            var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == order.PlanId);
            if (plan == null)
                modelState.AddModelError(nameof(Order.PlanId), "The selected plan does not exist.");
            else if (!plan.IsActive)
                modelState.AddModelError(nameof(Order.PlanId), "The selected plan is not active.");
            else if (plan.ConnectionType != order.ConnectionType)
                modelState.AddModelError(nameof(Order.PlanId), "The selected plan does not belong to the selected connection type.");
        }

        // --- Dial-up only: new landline plan ------------------------------------------------------------------
        if (order.LandlinePlanId.HasValue)
        {
            if (order.ConnectionType != ConnectionType.DialUp)
            {
                modelState.AddModelError(nameof(Order.LandlinePlanId), "A landline plan can only be chosen for a dial-up order.");
            }
            else
            {
                var landlinePlan = await db.Plans.FirstOrDefaultAsync(p => p.Id == order.LandlinePlanId.Value);
                if (landlinePlan == null || !landlinePlan.IsActive || landlinePlan.ConnectionType != ConnectionType.TelephoneOnly)
                    modelState.AddModelError(nameof(Order.LandlinePlanId), "Select a valid, active telephone (landline) plan.");
            }
        }

        // --- Dial-up only: existing landline of the same customer ---------------------------------------------
        if (order.ExistingLandlineConnectionId.HasValue)
        {
            if (order.ConnectionType != ConnectionType.DialUp)
            {
                modelState.AddModelError(nameof(Order.ExistingLandlineConnectionId), "An existing landline can only be chosen for a dial-up order.");
            }
            else
            {
                var landline = await db.Connections.FirstOrDefaultAsync(c => c.Id == order.ExistingLandlineConnectionId.Value);
                if (landline == null || landline.ConnectionType != ConnectionType.TelephoneOnly)
                    modelState.AddModelError(nameof(Order.ExistingLandlineConnectionId), "Select a valid existing landline account.");
                else if (landline.Status != ConnectionStatus.Active)
                    modelState.AddModelError(nameof(Order.ExistingLandlineConnectionId), "The selected landline connection is not active.");
                else if (landline.CustomerId != order.CustomerId)
                    modelState.AddModelError(nameof(Order.ExistingLandlineConnectionId), "The selected landline belongs to a different customer.");
            }
        }

        // --- Equipment: must exist; internet connections need a modem or a router -----------------------------
        if (order.EquipmentProductId.HasValue)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == order.EquipmentProductId.Value);
            if (product == null)
            {
                modelState.AddModelError(nameof(Order.EquipmentProductId), "The selected equipment does not exist.");
            }
            else if ((order.ConnectionType == ConnectionType.DialUp || order.ConnectionType == ConnectionType.Broadband)
                     && product.Category != ProductCategory.Modem && product.Category != ProductCategory.Router)
            {
                modelState.AddModelError(nameof(Order.EquipmentProductId), "An internet connection needs a modem or a router.");
            }
        }
    }
}
