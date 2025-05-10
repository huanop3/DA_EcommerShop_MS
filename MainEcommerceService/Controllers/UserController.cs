using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MainEcommerceService.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
//using MainEcommerceService.Models;

namespace MainEcommerceService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("LoginUser")]
        public async Task<IActionResult> LoginUser(LoginRequestVM userLoginVM)
        {
            var response = await _userService.Login(userLoginVM);
            if (response.Success)
            {
                return Ok(response);
            }
            else
            {
                return BadRequest(response);
            }
        }
        [HttpPost("RegisterUser")]
        public async Task<IActionResult> RegisterUser(RegisterLoginVM registerLoginVM)
        {
            var response = await _userService.Register(registerLoginVM);
            if (response.Success)
            {
                return Ok(response);
            }
            else
            {
                return BadRequest(response);
            }
        }
        [HttpPost("refresh-Token")]
        public async Task<IActionResult> RefreshToken(string token)
        {
            var response = await _userService.RefreshToken(token);
            if (response.Success)
            {
                return Ok(response);
            }
            else
            {
                return BadRequest(response);
            }
        }

    }
}