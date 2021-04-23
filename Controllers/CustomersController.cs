﻿using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Customers;
using Nop.Core.Infrastructure;
using Nop.Plugin.Api.Attributes;
using Nop.Plugin.Api.Delta;
using Nop.Plugin.Api.DTO;
using Nop.Plugin.Api.DTO.Customers;
using Nop.Plugin.Api.DTO.Errors;
using Nop.Plugin.Api.Factories;
using Nop.Plugin.Api.Helpers;
using Nop.Plugin.Api.JSON.ActionResults;
using Nop.Plugin.Api.JSON.Serializers;
using Nop.Plugin.Api.MappingExtensions;
using Nop.Plugin.Api.ModelBinders;
using Nop.Plugin.Api.Models.CustomersParameters;
using Nop.Plugin.Api.Services;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Services.Stores;
using System;
using System.Linq;
using System.Net;
using static Nop.Plugin.Api.Infrastructure.Constants;

namespace Nop.Plugin.Api.Controllers
{
    public class CustomersController : BaseApiController
    {
        private readonly ICustomerApiService _customerApiService;
        private readonly ICustomerRolesHelper _customerRolesHelper;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IEncryptionService _encryptionService;
        private readonly ICountryService _countryService;
        private readonly IMappingHelper _mappingHelper;
        private readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;
        private readonly ILanguageService _languageService;
        private readonly IFactory<Customer> _factory;
        private readonly IDTOHelper _dtoHelper;
        private readonly IAddressService _addressService;

        // We resolve the customer settings this way because of the tests.
        // The auto mocking does not support concreate types as dependencies. It supports only interfaces.
        private CustomerSettings _customerSettings;

        private CustomerSettings CustomerSettings
        {
            get
            {
                if (_customerSettings == null)
                {
                    _customerSettings = EngineContext.Current.Resolve<CustomerSettings>();
                }

                return _customerSettings;
            }
        }

        public CustomersController(
            ICustomerApiService customerApiService,
            IJsonFieldsSerializer jsonFieldsSerializer,
            IAclService aclService,
            ICustomerService customerService,
            IStoreMappingService storeMappingService,
            IStoreService storeService,
            IDiscountService discountService,
            ICustomerActivityService customerActivityService,
            ILocalizationService localizationService,
            ICustomerRolesHelper customerRolesHelper,
            IGenericAttributeService genericAttributeService,
            IEncryptionService encryptionService,
            IFactory<Customer> factory,
            ICountryService countryService,
            IMappingHelper mappingHelper,
            IPluginService pluginService,
            INewsLetterSubscriptionService newsLetterSubscriptionService,
            IPictureService pictureService,
            ILanguageService languageService,
            IDTOHelper dtoHelper,
            IAddressService addressService) :
            base(jsonFieldsSerializer,
                aclService,
                customerService,
                storeMappingService,
                storeService,
                discountService,
                customerActivityService,
                localizationService,
                pictureService)
        {
            _customerApiService = customerApiService;
            _factory = factory;
            _countryService = countryService;
            _mappingHelper = mappingHelper;
            _newsLetterSubscriptionService = newsLetterSubscriptionService;
            _languageService = languageService;
            _encryptionService = encryptionService;
            _genericAttributeService = genericAttributeService;
            _customerRolesHelper = customerRolesHelper;
            _dtoHelper = dtoHelper;
            _addressService = addressService;
        }

        /// <summary>
        /// Retrieve all customers of a shop
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="400">Bad request</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [Route("/api/customers")]
        [ProducesResponseType(typeof(CustomersRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        [GetRequestsErrorInterceptorActionFilter]
        public IActionResult GetCustomers(CustomersParametersModel parameters)
        {
            if (parameters.Limit < Configurations.MinLimit || parameters.Limit > Configurations.MaxLimit)
            {
                return Error(HttpStatusCode.BadRequest, "limit", "Invalid limit parameter");
            }

            if (parameters.Page < Configurations.DefaultPageValue)
            {
                return Error(HttpStatusCode.BadRequest, "page", "Invalid request parameters");
            }

            var allCustomers = _customerApiService.GetCustomersDtos(parameters.CreatedAtMin,
                parameters.CreatedAtMax,
                parameters.Limit,
                parameters.Page,
                parameters.SinceId);

            var customersRootObject = new CustomersRootObject() {Customers = allCustomers};

            var json = JsonFieldsSerializer.Serialize(customersRootObject, parameters.Fields);

            return new RawJsonActionResult(json);
        }

        /// <summary>
        /// Retrieve customer by spcified id
        /// </summary>
        /// <param name="id">Id of the customer</param>
        /// <param name="fields">Fields from the customer you want your json to contain</param>
        /// <response code="200">OK</response>
        /// <response code="404">Not Found</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [Route("/api/customers/{id}")]
        [ProducesResponseType(typeof(CustomersRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        [GetRequestsErrorInterceptorActionFilter]
        public IActionResult GetCustomerById(int id, string fields = "")
        {
            if (id <= 0)
            {
                return Error(HttpStatusCode.BadRequest, "id", "invalid id");
            }

            var customer = _customerApiService.GetCustomerById(id);

            if (customer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            var customersRootObject = new CustomersRootObject();
            customersRootObject.Customers.Add(customer);

            var json = JsonFieldsSerializer.Serialize(customersRootObject, fields);

            return new RawJsonActionResult(json);
        }


        /// <summary>
        /// Get a count of all customers
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [Route("/api/customers/count")]
        [ProducesResponseType(typeof(CustomersCountRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        public IActionResult GetCustomersCount()
        {
            var allCustomersCount = _customerApiService.GetCustomersCount();

            var customersCountRootObject = new CustomersCountRootObject() {Count = allCustomersCount};

            return Ok(customersCountRootObject);
        }

        /// <summary>
        /// Search for customers matching supplied query
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="400">Bad Request</response>
        /// <response code="401">Unauthorized</response>
        [HttpGet]
        [Route("/api/customers/search")]
        [ProducesResponseType(typeof(CustomersRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        public IActionResult Search(CustomersSearchParametersModel parameters)
        {
            if (parameters.Limit <= Configurations.MinLimit || parameters.Limit > Configurations.MaxLimit)
            {
                return Error(HttpStatusCode.BadRequest, "limit", "Invalid limit parameter");
            }

            if (parameters.Page <= 0)
            {
                return Error(HttpStatusCode.BadRequest, "page", "Invalid page parameter");
            }

            var customersDto = _customerApiService.Search(parameters.Query, parameters.Order, parameters.Page, parameters.Limit);

            var customersRootObject = new CustomersRootObject() {Customers = customersDto};

            var json = JsonFieldsSerializer.Serialize(customersRootObject, parameters.Fields);

            return new RawJsonActionResult(json);
        }

        [HttpPost]
        [Route("/api/customers")]
        [ProducesResponseType(typeof(CustomersRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorsRootObject), 422)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        public IActionResult CreateCustomer([ModelBinder(typeof(JsonModelBinder<CustomerDto>))]
            Delta<CustomerDto> customerDelta)
        {
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return Error();
            }

            var customer = CustomerService.GetCustomerByEmail(customerDelta.Dto.Email);
            if (customer != null && !customer.Deleted)
                return Error(HttpStatusCode.Conflict, nameof(Customer.Email), "Email is already registered");

            //If the validation has passed the customerDelta object won't be null for sure so we don't need to check for this.

            // Inserting the new customer
            var newCustomer = _factory.Initialize();
            customerDelta.Merge(newCustomer);

            // majako changes: need to create customer then address then customerAddress so their Ids wont be 0
            CustomerService.InsertCustomer(newCustomer);

            // majako changes: remove these customerDelta.Dto.Addresses since they are vague, provide no billing or shipping information

            // majako changes: insert customer billing and shipping addresses
            if (customerDelta.Dto.BillingAddress != null)
            {
                var billingAddress = customerDelta.Dto.BillingAddress.ToEntity();
                _addressService.InsertAddress(billingAddress);
                newCustomer.BillingAddressId = billingAddress.Id;
                // we need to explicitly set the date as if it is not specified
                // it will default to 01/01/0001 which is not supported by SQL Server and throws and exception
                if (customerDelta.Dto.BillingAddress.CreatedOnUtc == null)
                {
                    billingAddress.CreatedOnUtc = DateTime.UtcNow;
                }

                CustomerService.InsertCustomerAddress(newCustomer, billingAddress);
            }

            if (customerDelta.Dto.ShippingAddress != null)
            {
                var shippingAddress = customerDelta.Dto.ShippingAddress.ToEntity();
                _addressService.InsertAddress(shippingAddress);
                newCustomer.ShippingAddressId = shippingAddress.Id;
                // we need to explicitly set the date as if it is not specified
                // it will default to 01/01/0001 which is not supported by SQL Server and throws and exception
                if (customerDelta.Dto.ShippingAddress.CreatedOnUtc == null)
                {
                    shippingAddress.CreatedOnUtc = DateTime.UtcNow;
                }

                CustomerService.InsertCustomerAddress(newCustomer, shippingAddress);
            }

            CustomerService.UpdateCustomer(newCustomer);

            InsertFirstAndLastNameGenericAttributes(customerDelta.Dto.FirstName, customerDelta.Dto.LastName, newCustomer);

            if (!string.IsNullOrEmpty(customerDelta.Dto.LanguageId) && int.TryParse(customerDelta.Dto.LanguageId, out var languageId)
                                                                    && _languageService.GetLanguageById(languageId) != null)
            {
                _genericAttributeService.SaveAttribute(newCustomer, NopCustomerDefaults.LanguageIdAttribute, languageId);
            }

            //password
            if (!string.IsNullOrWhiteSpace(customerDelta.Dto.Password))
            {
                AddPassword(customerDelta.Dto.Password, newCustomer);
            }

            // We need to insert the entity first so we can have its id in order to map it to anything.
            // TODO: Localization
            // TODO: move this before inserting the customer.
            if (customerDelta.Dto.RoleIds.Count > 0)
            {
                AddValidRoles(customerDelta, newCustomer);
            }

            // Preparing the result dto of the new customer
            // We do not prepare the shopping cart items because we have a separate endpoint for them.
            var newCustomerDto = _dtoHelper.PrepareCustomerDTO(newCustomer);

            // This is needed because the entity framework won't populate the navigation properties automatically
            // and the country will be left null. So we do it by hand here.
            PopulateAddressCountryNames(newCustomerDto);

            // Set the fist and last name separately because they are not part of the customer entity, but are saved in the generic attributes.
            newCustomerDto.FirstName = customerDelta.Dto.FirstName;
            newCustomerDto.LastName = customerDelta.Dto.LastName;

            newCustomerDto.LanguageId = customerDelta.Dto.LanguageId;

            //activity log
            CustomerActivityService.InsertActivity("AddNewCustomer", LocalizationService.GetResource("ActivityLog.AddNewCustomer"), newCustomer);

            var customersRootObject = new CustomersRootObject();

            customersRootObject.Customers.Add(newCustomerDto);

            var json = JsonFieldsSerializer.Serialize(customersRootObject, string.Empty);

            return new RawJsonActionResult(json);
        }

        [HttpPut]
        [Route("/api/customers/{id}")]
        [ProducesResponseType(typeof(CustomersRootObject), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorsRootObject), 422)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        public IActionResult UpdateCustomer([ModelBinder(typeof(JsonModelBinder<CustomerDto>))]
            Delta<CustomerDto> customerDelta)
        {
            // Here we display the errors if the validation has failed at some point.
            if (!ModelState.IsValid)
            {
                return Error();
            }

            // Updateting the customer
            var currentCustomer = _customerApiService.GetCustomerEntityById(customerDelta.Dto.Id);

            if (currentCustomer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            customerDelta.Merge(currentCustomer);

            if (customerDelta.Dto.RoleIds.Count > 0)
            {
                AddValidRoles(customerDelta, currentCustomer);
            }

            if (customerDelta.Dto.Addresses.Count > 0)
            {
                var currentCustomerAddresses = CustomerService.GetAddressesByCustomerId(currentCustomer.Id).ToDictionary(address => address.Id, address => address);

                foreach (var passedAddress in customerDelta.Dto.Addresses)
                {
                    var addressEntity = passedAddress.ToEntity();

                    // majako changes: ensure addresses exists
                    if (passedAddress.Id == 0)
                    {
                        _addressService.InsertAddress(addressEntity);
                        passedAddress.Id = addressEntity.Id;
                    }

                    if (currentCustomerAddresses.ContainsKey(passedAddress.Id))
                    {
                        _mappingHelper.Merge(passedAddress, currentCustomerAddresses[passedAddress.Id]);
                    }
                    else
                    {
                        CustomerService.InsertCustomerAddress(currentCustomer, addressEntity);
                    }
                }

                // majako changes: if address are enusred above, then we can map the new Id to customer billing and shipping dto, orderly
                customerDelta.Dto.BillingAddress.Id = customerDelta.Dto.Addresses.First().Id;
                customerDelta.Dto.ShippingAddress.Id = customerDelta.Dto.Addresses.Last().Id;
            }

            // majako changes: insert/update customer billing and shipping addresses mapping if not exists
            // this is due to Order will get address from these
            if (CustomerService.GetCustomerBillingAddress(currentCustomer) == null)
            {
                if (currentCustomer.BillingAddressId > 0)
                {
                    CustomerService.InsertCustomerAddress(currentCustomer, _addressService.GetAddressById(currentCustomer.BillingAddressId.Value));
                }
                else
                {
                    // majako changes: insert customer billing and shipping addresses
                    if (customerDelta.Dto.BillingAddress != null)
                    {
                        var billingAddress = customerDelta.Dto.BillingAddress.ToEntity();
                        _addressService.InsertAddress(billingAddress);
                        currentCustomer.BillingAddressId = billingAddress.Id;
                        // we need to explicitly set the date as if it is not specified
                        // it will default to 01/01/0001 which is not supported by SQL Server and throws and exception
                        if (customerDelta.Dto.BillingAddress.CreatedOnUtc == null)
                        {
                            billingAddress.CreatedOnUtc = DateTime.UtcNow;
                        }

                        CustomerService.InsertCustomerAddress(currentCustomer, billingAddress);
                    }
                }
            }

            if (CustomerService.GetCustomerShippingAddress(currentCustomer) == null)
            {
                if (currentCustomer.ShippingAddressId > 0)
                {
                    CustomerService.InsertCustomerAddress(currentCustomer, _addressService.GetAddressById(currentCustomer.ShippingAddressId.Value));
                }
                else
                {
                    // majako changes: insert customer billing and shipping addresses
                    if (customerDelta.Dto.ShippingAddress != null)
                    {
                        var shippingAddress = customerDelta.Dto.ShippingAddress.ToEntity();
                        _addressService.InsertAddress(shippingAddress);
                        currentCustomer.ShippingAddressId = shippingAddress.Id;
                        // we need to explicitly set the date as if it is not specified
                        // it will default to 01/01/0001 which is not supported by SQL Server and throws and exception
                        if (customerDelta.Dto.ShippingAddress.CreatedOnUtc == null)
                        {
                            shippingAddress.CreatedOnUtc = DateTime.UtcNow;
                        }

                        CustomerService.InsertCustomerAddress(currentCustomer, shippingAddress);
                    }
                }
            }

            if (customerDelta.Dto.BillingAddress != null && customerDelta.Dto.BillingAddress.Id > 0)
                currentCustomer.BillingAddressId = customerDelta.Dto.BillingAddress.Id;

            if (customerDelta.Dto.ShippingAddress != null && customerDelta.Dto.ShippingAddress.Id > 0)
                currentCustomer.ShippingAddressId = customerDelta.Dto.ShippingAddress.Id;

            CustomerService.UpdateCustomer(currentCustomer);

            InsertFirstAndLastNameGenericAttributes(customerDelta.Dto.FirstName, customerDelta.Dto.LastName, currentCustomer);


            if (!string.IsNullOrEmpty(customerDelta.Dto.LanguageId) && int.TryParse(customerDelta.Dto.LanguageId, out var languageId)
                                                                    && _languageService.GetLanguageById(languageId) != null)
            {
                _genericAttributeService.SaveAttribute(currentCustomer, NopCustomerDefaults.LanguageIdAttribute, languageId);
            }

            //password
            if (!string.IsNullOrWhiteSpace(customerDelta.Dto.Password))
            {
                AddPassword(customerDelta.Dto.Password, currentCustomer);
            }

            // TODO: Localization

            // Preparing the result dto of the new customer
            // We do not prepare the shopping cart items because we have a separate endpoint for them.
            var updatedCustomer = _dtoHelper.PrepareCustomerDTO(currentCustomer);

            // This is needed because the entity framework won't populate the navigation properties automatically
            // and the country name will be left empty because the mapping depends on the navigation property
            // so we do it by hand here.
            PopulateAddressCountryNames(updatedCustomer);

            // Set the fist and last name separately because they are not part of the customer entity, but are saved in the generic attributes.
            var firstNameGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "FirstName");

            if (firstNameGenericAttribute != null)
            {
                updatedCustomer.FirstName = firstNameGenericAttribute.Value;
            }

            var lastNameGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "LastName");

            if (lastNameGenericAttribute != null)
            {
                updatedCustomer.LastName = lastNameGenericAttribute.Value;
            }

            var languageIdGenericAttribute = _genericAttributeService.GetAttributesForEntity(currentCustomer.Id, typeof(Customer).Name)
                .FirstOrDefault(x => x.Key == "LanguageId");

            if (languageIdGenericAttribute != null)
            {
                updatedCustomer.LanguageId = languageIdGenericAttribute.Value;
            }

            //activity log
            CustomerActivityService.InsertActivity("UpdateCustomer", LocalizationService.GetResource("ActivityLog.UpdateCustomer"), currentCustomer);

            var customersRootObject = new CustomersRootObject();

            customersRootObject.Customers.Add(updatedCustomer);

            var json = JsonFieldsSerializer.Serialize(customersRootObject, string.Empty);

            return new RawJsonActionResult(json);
        }

        [HttpDelete]
        [Route("/api/customers/{id}")]
        [GetRequestsErrorInterceptorActionFilter]
        [ProducesResponseType(typeof(void), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorsRootObject), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.Unauthorized)]
        public IActionResult DeleteCustomer(int id)
        {
            if (id <= 0)
            {
                return Error(HttpStatusCode.BadRequest, "id", "invalid id");
            }

            var customer = _customerApiService.GetCustomerEntityById(id);

            if (customer == null)
            {
                return Error(HttpStatusCode.NotFound, "customer", "not found");
            }

            CustomerService.DeleteCustomer(customer);

            //remove newsletter subscription (if exists)
            foreach (var store in StoreService.GetAllStores())
            {
                var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(customer.Email, store.Id);
                if (subscription != null)
                    _newsLetterSubscriptionService.DeleteNewsLetterSubscription(subscription);
            }

            //activity log
            CustomerActivityService.InsertActivity("DeleteCustomer", LocalizationService.GetResource("ActivityLog.DeleteCustomer"), customer);

            return new RawJsonActionResult("{}");
        }

        private void InsertFirstAndLastNameGenericAttributes(string firstName, string lastName, Customer newCustomer)
        {
            // we assume that if the first name is not sent then it will be null and in this case we don't want to update it
            if (firstName != null)
            {
                _genericAttributeService.SaveAttribute(newCustomer, NopCustomerDefaults.FirstNameAttribute, firstName);
            }

            if (lastName != null)
            {
                _genericAttributeService.SaveAttribute(newCustomer, NopCustomerDefaults.LastNameAttribute, lastName);
            }
        }

        private void AddValidRoles(Delta<CustomerDto> customerDelta, Customer currentCustomer)
        {
            var allCustomerRoles = CustomerService.GetAllCustomerRoles(true);
            foreach (var customerRole in allCustomerRoles)
            {
                if (string.IsNullOrEmpty(customerRole.SystemName)) continue;
                if (customerDelta.Dto.RoleIds.Contains(customerRole.Id))
                {
                    //new role
                    if (!CustomerService.IsInCustomerRole(currentCustomer, customerRole.SystemName))
                    {
                        CustomerService.AddCustomerRoleMapping(new CustomerCustomerRoleMapping() {CustomerId = currentCustomer.Id, CustomerRoleId = customerRole.Id});
                    }
                }
                else
                {
                    if (CustomerService.IsInCustomerRole(currentCustomer, customerRole.SystemName))
                    {
                        CustomerService.RemoveCustomerRoleMapping(currentCustomer, customerRole);
                    }
                }
            }
        }

        private void PopulateAddressCountryNames(CustomerDto newCustomerDto)
        {
            foreach (var address in newCustomerDto.Addresses)
            {
                SetCountryName(address);
            }

            if (newCustomerDto.BillingAddress != null)
            {
                SetCountryName(newCustomerDto.BillingAddress);
            }

            if (newCustomerDto.ShippingAddress != null)
            {
                SetCountryName(newCustomerDto.ShippingAddress);
            }
        }

        private void SetCountryName(AddressDto address)
        {
            if (string.IsNullOrEmpty(address.CountryName) && address.CountryId.HasValue)
            {
                var country = _countryService.GetCountryById(address.CountryId.Value);
                address.CountryName = country.Name;
            }
        }

        private void AddPassword(string newPassword, Customer customer)
        {
            // TODO: call this method before inserting the customer.
            var customerPassword = new CustomerPassword {CustomerId = customer.Id, PasswordFormat = CustomerSettings.DefaultPasswordFormat, CreatedOnUtc = DateTime.UtcNow};

            switch (CustomerSettings.DefaultPasswordFormat)
            {
                case PasswordFormat.Clear:
                {
                    customerPassword.Password = newPassword;
                }
                    break;
                case PasswordFormat.Encrypted:
                {
                    customerPassword.Password = _encryptionService.EncryptText(newPassword);
                }
                    break;
                case PasswordFormat.Hashed:
                {
                    var saltKey = _encryptionService.CreateSaltKey(5);
                    customerPassword.PasswordSalt = saltKey;
                    customerPassword.Password = _encryptionService.CreatePasswordHash(newPassword, saltKey, CustomerSettings.HashedPasswordFormat);
                }
                    break;
            }

            CustomerService.InsertCustomerPassword(customerPassword);

            CustomerService.UpdateCustomer(customer);
        }
    }
}