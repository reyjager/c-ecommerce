using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcProject.Data;
using MyMvcProject.Models;

namespace MyMvcProject.Controllers
{
    public class BranchController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BranchController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Branch
        public async Task<IActionResult> Index()
        {
            var branches = await _context.Branches.ToListAsync();
            return View(branches);
        }

        // GET: Branch/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (branch == null)
            {
                return NotFound();
            }

            return View(branch);
        }

        // GET: Branch/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Branch/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BranchId,BranchName,BranchAddress,ContactNumber,LocationsString,IsActive")] Branch branch)
        {
            if (ModelState.IsValid)
            {
                // Check if BranchId already exists
                if (await _context.Branches.AnyAsync(b => b.BranchId == branch.BranchId))
                {
                    ModelState.AddModelError("BranchId", "Branch ID already exists.");
                    return View(branch);
                }

                branch.DateCreated = DateTime.Now;
                branch.IsActive = true;
                
                _context.Add(branch);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Branch created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(branch);
        }

        // GET: Branch/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches.FindAsync(id);
            if (branch == null)
            {
                return NotFound();
            }
            return View(branch);
        }

        // POST: Branch/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,BranchId,BranchName,BranchAddress,ContactNumber,LocationsString,IsActive")] Branch branch)
        {
            if (id != branch.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if BranchId already exists for another branch
                    if (await _context.Branches.AnyAsync(b => b.BranchId == branch.BranchId && b.Id != branch.Id))
                    {
                        ModelState.AddModelError("BranchId", "Branch ID already exists.");
                        return View(branch);
                    }

                    // Get the existing branch to preserve DateCreated
                    var existingBranch = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
                    if (existingBranch != null)
                    {
                        branch.DateCreated = existingBranch.DateCreated;
                    }

                    branch.DateUpdated = DateTime.Now;
                    
                    _context.Update(branch);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Branch updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BranchExists(branch.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(branch);
        }

        // GET: Branch/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (branch == null)
            {
                return NotFound();
            }

            return View(branch);
        }

        // POST: Branch/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            
            if (branch == null)
            {
                return NotFound();
            }
            
            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Branch deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Branch/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            
            if (branch == null)
            {
                return NotFound();
            }
            
            branch.IsActive = !branch.IsActive;
            branch.DateUpdated = DateTime.Now;
            
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"Branch {(branch.IsActive ? "activated" : "deactivated")} successfully!";
            return RedirectToAction(nameof(Index));
        }

        private bool BranchExists(int id)
        {
            return _context.Branches.Any(e => e.Id == id);
        }
    }
}