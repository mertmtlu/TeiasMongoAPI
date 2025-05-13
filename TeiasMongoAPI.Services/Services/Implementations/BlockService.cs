using AutoMapper;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Block;
using TeiasMongoAPI.Services.DTOs.Request.Block;
using TeiasMongoAPI.Services.DTOs.Response.Block;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class BlockService : BaseService, IBlockService
    {
        public BlockService(IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper)
        {
        }

        public async Task<BlockDto> GetBlockAsync(string buildingId, string blockId, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var block = await _unitOfWork.Buildings.GetBlockAsync(buildingObjectId, blockId, cancellationToken);

            if (block == null)
            {
                throw new KeyNotFoundException($"Block with ID {blockId} not found in building {buildingId}.");
            }

            return _mapper.Map<BlockDto>(block);
        }

        public async Task<List<BlockDto>> GetBlocksAsync(string buildingId, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var blocks = await _unitOfWork.Buildings.GetBlocksAsync(buildingObjectId, cancellationToken);

            return _mapper.Map<List<BlockDto>>(blocks);
        }

        public async Task<List<ConcreteBlockDto>> GetConcreteBlocksAsync(string buildingId, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var concreteBlocks = await _unitOfWork.Buildings.GetBlocksByTypeAsync<Concrete>(buildingObjectId, cancellationToken);

            return _mapper.Map<List<ConcreteBlockDto>>(concreteBlocks);
        }

        public async Task<List<MasonryBlockDto>> GetMasonryBlocksAsync(string buildingId, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var masonryBlocks = await _unitOfWork.Buildings.GetBlocksByTypeAsync<Masonry>(buildingObjectId, cancellationToken);

            return _mapper.Map<List<MasonryBlockDto>>(masonryBlocks);
        }

        public async Task<ConcreteBlockDto> CreateConcreteBlockAsync(string buildingId, ConcreteCreateDto dto, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var building = await _unitOfWork.Buildings.GetByIdAsync(buildingObjectId, cancellationToken);

            if (building == null)
            {
                throw new KeyNotFoundException($"Building with ID {buildingId} not found.");
            }

            // Check if block with same ID already exists
            if (building.Blocks.Any(b => b.ID == dto.ID))
            {
                throw new InvalidOperationException($"Block with ID {dto.ID} already exists in the building.");
            }

            var concreteBlock = _mapper.Map<Concrete>(dto);
            var success = await _unitOfWork.Buildings.AddBlockAsync(buildingObjectId, concreteBlock, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to add concrete block to building {buildingId}.");
            }

            // Get the updated building to return the created block
            var updatedBuilding = await _unitOfWork.Buildings.GetByIdAsync(buildingObjectId, cancellationToken);
            var createdBlock = updatedBuilding.Blocks.OfType<Concrete>().FirstOrDefault(b => b.ID == dto.ID);

            return _mapper.Map<ConcreteBlockDto>(createdBlock);
        }

        public async Task<MasonryBlockDto> CreateMasonryBlockAsync(string buildingId, MasonryCreateDto dto, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var building = await _unitOfWork.Buildings.GetByIdAsync(buildingObjectId, cancellationToken);

            if (building == null)
            {
                throw new KeyNotFoundException($"Building with ID {buildingId} not found.");
            }

            // Check if block with same ID already exists
            if (building.Blocks.Any(b => b.ID == dto.ID))
            {
                throw new InvalidOperationException($"Block with ID {dto.ID} already exists in the building.");
            }

            var masonryBlock = _mapper.Map<Masonry>(dto);
            var success = await _unitOfWork.Buildings.AddBlockAsync(buildingObjectId, masonryBlock, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to add masonry block to building {buildingId}.");
            }

            // Get the updated building to return the created block
            var updatedBuilding = await _unitOfWork.Buildings.GetByIdAsync(buildingObjectId, cancellationToken);
            var createdBlock = updatedBuilding.Blocks.OfType<Masonry>().FirstOrDefault(b => b.ID == dto.ID);

            return _mapper.Map<MasonryBlockDto>(createdBlock);
        }

        public async Task<ConcreteBlockDto> UpdateConcreteBlockAsync(string buildingId, string blockId, ConcreteUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var block = await _unitOfWork.Buildings.GetBlockAsync(buildingObjectId, blockId, cancellationToken);

            if (block == null)
            {
                throw new KeyNotFoundException($"Block with ID {blockId} not found in building {buildingId}.");
            }

            if (block is not Concrete concreteBlock)
            {
                throw new InvalidOperationException($"Block with ID {blockId} is not a concrete block.");
            }

            // If updating ID, check uniqueness
            if (!string.IsNullOrEmpty(dto.ID) && dto.ID != blockId)
            {
                var building = await _unitOfWork.Buildings.GetByIdAsync(buildingObjectId, cancellationToken);
                if (building.Blocks.Any(b => b.ID == dto.ID))
                {
                    throw new InvalidOperationException($"Block with ID {dto.ID} already exists in the building.");
                }
            }

            _mapper.Map(dto, concreteBlock);
            var success = await _unitOfWork.Buildings.UpdateBlockAsync(buildingObjectId, blockId, concreteBlock, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update concrete block {blockId} in building {buildingId}.");
            }

            return _mapper.Map<ConcreteBlockDto>(concreteBlock);
        }

        public async Task<MasonryBlockDto> UpdateMasonryBlockAsync(string buildingId, string blockId, MasonryUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var block = await _unitOfWork.Buildings.GetBlockAsync(buildingObjectId, blockId, cancellationToken);

            if (block == null)
            {
                throw new KeyNotFoundException($"Block with ID {blockId} not found in building {buildingId}.");
            }

            if (block is not Masonry masonryBlock)
            {
                throw new InvalidOperationException($"Block with ID {blockId} is not a masonry block.");
            }

            // If updating ID, check uniqueness
            if (!string.IsNullOrEmpty(dto.ID) && dto.ID != blockId)
            {
                var building = await _unitOfWork.Buildings.GetByIdAsync(buildingObjectId, cancellationToken);
                if (building.Blocks.Any(b => b.ID == dto.ID))
                {
                    throw new InvalidOperationException($"Block with ID {dto.ID} already exists in the building.");
                }
            }

            _mapper.Map(dto, masonryBlock);
            var success = await _unitOfWork.Buildings.UpdateBlockAsync(buildingObjectId, blockId, masonryBlock, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update masonry block {blockId} in building {buildingId}.");
            }

            return _mapper.Map<MasonryBlockDto>(masonryBlock);
        }

        public async Task<bool> DeleteBlockAsync(string buildingId, string blockId, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var block = await _unitOfWork.Buildings.GetBlockAsync(buildingObjectId, blockId, cancellationToken);

            if (block == null)
            {
                throw new KeyNotFoundException($"Block with ID {blockId} not found in building {buildingId}.");
            }

            return await _unitOfWork.Buildings.RemoveBlockAsync(buildingObjectId, blockId, cancellationToken);
        }

        public async Task<BlockSummaryDto> GetBlockSummaryAsync(string buildingId, string blockId, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var block = await _unitOfWork.Buildings.GetBlockAsync(buildingObjectId, blockId, cancellationToken);

            if (block == null)
            {
                throw new KeyNotFoundException($"Block with ID {blockId} not found in building {buildingId}.");
            }

            return _mapper.Map<BlockSummaryDto>(block);
        }
    }
}