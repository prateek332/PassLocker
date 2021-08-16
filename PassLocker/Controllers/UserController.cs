﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassLocker.Dto;
using PassLocker.Services.Protector;
using PassLockerDatabase;

namespace PassLocker.Controllers
{
    [ApiController]
    [Route("api/[Controller]")]
    public class UserController : ControllerBase
    {
        private readonly PassLockerDbContext _db;
        private readonly IProtector _protector;

        public UserController(PassLockerDbContext dbContext, IProtector protector)
        {
            _db = dbContext;
            _protector = protector;
        }

        // GET: api/user/all-users
        [HttpGet("all-users")]
        [ProducesResponseType(404)]
        public async Task<ActionResult<UserViewDto>> Get()
        {
            var allUsers = await _db.Users.ToListAsync();
            var users = allUsers.Select(UserToDto).ToList();
            return Ok(users);
        }


        // GET: api/user/{id}
        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(UserViewDto))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<UserViewDto>> GetUser(string id)
        {
            User user = await _db.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound("User does not exists");
            }

            return UserToDto(user);
        }

        // POST: api/user/create-user
        [HttpPost("create-user")]
        [ProducesResponseType(201, Type = typeof(GoogleBasicUserProfile))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> CreateUser([FromBody] GoogleBasicUserProfile user)
        {
            if (user == null)
            {
                return BadRequest("Invalid request content. User's info is invalid");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (hashedPassword, passwordSalt) = _protector.CreateHashedStringAndSalt(
                user.Password);
            var (hashedSecret, secretSalt) = _protector.CreateHashedStringAndSalt(
                user.Secret);

            user.Password = hashedPassword;
            user.Secret = hashedSecret;

            var newUser = GoogleUserToDatabaseDto(user, passwordSalt, secretSalt);

            await _db.Users.AddAsync(newUser);
            int affected = await _db.SaveChangesAsync();
            if (affected == 1)
            {
                return Created(nameof(GetUser), UserToDto(newUser));
            }
            else
            {
                return Problem("Some problem at the server. Cannot create new user.");
            }
        }

        [HttpPut("{id}/edit-profile")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] User user)
        {
            if (user == null || !user.UserId.Equals(id))
            {
                return BadRequest("Invalid request content. User's info is invalid");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // protector.CreateHashedStringAndSalt(user.UserPassword);

            User newUser = UserToDatabaseDto(user);

            _db.Users.Update(newUser);
            int affected = await _db.SaveChangesAsync();
            if (affected == 1)
            {
                return NoContent();
            }
            return NotFound("User could not be found in database");
        }

        [HttpDelete("{id}/delete-user")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            User user = await _db.Users.FindAsync(id);
            if (user == null)
            {
                return BadRequest("User does not exists");
            }

            _db.Users.Remove(user);
            var affected = await _db.SaveChangesAsync();
            if (affected == 1)
            {
                return NoContent();
            }
            return NotFound("User could not be found in database");
        }

        private static UserViewDto UserToDto(User user) =>
            new UserViewDto
            {
                UserId = user.UserId,
                UserEmail = user.UserEmail,
                UserName = user.UserName,
                Confirmed = user.UserConfirmed,
                Name = user.Name,
                Gender = user.Gender,
                MemberSince = user.MemberSince
            };

        private static User UserToDatabaseDto(User user) =>
            new User
            {
                UserId = user.UserId,
                UserName = user.UserName,
                UserEmail = user.UserEmail,
                UserPasswordSalt = user.UserPasswordSalt,
                UserPasswordHash = user.UserPasswordHash,
                UserSecretHash = user.UserSecretHash,
                UserConfirmed = user.UserConfirmed,
                Name = user.Name,
                Location = user.Location,
                Gender = user.Gender,
                MemberSince = user.MemberSince,
                Passwords = user.Passwords
            };

        private User GoogleUserToDatabaseDto(GoogleBasicUserProfile user,
            string passwordSalt, string secretSalt)
        {
            // creating a new user with details from front-end
            var newUser = new User
            {
                UserName = user.Username,
                UserEmail = user.Email,
                UserPasswordSalt = passwordSalt,
                UserPasswordHash = user.Password,
                UserSecretSalt = secretSalt,
                UserSecretHash = user.Secret,
                UserConfirmed = true,
                Name = user.Name,
                Location = user.Location,
                Gender = user.Gender,
                MemberSince = DateTime.Today.ToShortDateString()
            };
            
            // creating a unique uuid for user
            var uuid = _protector.GetUuid();
            newUser.UserId = uuid;

            return newUser;
        }
    }
}