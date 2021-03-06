//Includes for IntelliSense 
#define _SIZE_T_DEFINED

#include <cuda.h>
#include <device_launch_parameters.h>
#include <texture_fetch_functions.h>
#include "float.h"
#include <builtin_types.h>
#include <vector_functions.h>
#include <math.h>

#include "../NeuralNetwork/Activation/ActivationFunction.cu"

extern "C"
{
	typedef enum MyBackPropMethod
	{
		SGD = 0,
		RMSProp = 1,
	} MyBackPropMethod;

	__device__ void SGDWeightUpdate(float trainingRate, float momentum, float *weights, float *weightDeltas, int weightId, float gradient)
	{
		float weightDelta = trainingRate * gradient + momentum * weightDeltas[weightId];
		weightDeltas[weightId] = weightDelta;
		weights[weightId] += weightDelta;
	}

	__device__ void RMSPropWeightUpdate(float trainingRate, float momentum, float smoothingFactor, float *weights, float *weightDeltas, float *weightMeanSquares, int weightId, float gradient)
	{
		float rmsGradient = gradient + momentum * weightDeltas[weightId];
		weightDeltas[weightId] = rmsGradient;
		float weightMeanSquare = smoothingFactor * weightMeanSquares[weightId] + (1.0f - smoothingFactor) * rmsGradient * rmsGradient;
		if (weightMeanSquare != 0)
			rmsGradient /= sqrtf(weightMeanSquare);
		weightMeanSquares[weightId] = weightMeanSquare;
		weights[weightId] += trainingRate * rmsGradient;
	}

	__global__ void LSTMUpdateGateWeightsKernel(
		float *input,
		float *previousOutput,
		float *cellStates,
		float *cellStateErrors,
		float *outputGateDeltas,
		float *inputGateWeights,
		float *inputGateWeightDeltas,
		float *inputGateWeightMeanSquares,
		float *forgetGateWeights,
		float *forgetGateWeightDeltas,
		float *forgetGateWeightMeanSquares,
		float *outputGateWeights,
		float *outputGateWeightDeltas,
		float *outputGateWeightMeanSquares,
		float *inputGateWeightsRTRLPartials,
		float *forgetGateWeightsRTRLPartials,

		MyBackPropMethod backPropMethod,
		float trainingRate,
		float momentum,
		float smoothingFactor,

		int inputCount,
		int previousOutputCount,
		int cellsPerBlock
		)
	{
		int weightId = blockDim.x * blockIdx.y * gridDim.x	//rows preceeding current row in grid
			+ blockDim.x * blockIdx.x				//blocks preceeding current block
			+ threadIdx.x;

		int weightsPerGate = inputCount + previousOutputCount + cellsPerBlock + 1;

		if (weightId < weightsPerGate * previousOutputCount/cellsPerBlock)
		{
			int fromId = weightId % weightsPerGate;
			int toId = weightId / weightsPerGate;

			//calculate output gate weight gradient
			int isFromInputUnit = fromId >= 0 && fromId < inputCount;
			int isFromPreviousOutputUnit = (fromId >= inputCount) && (fromId < inputCount + previousOutputCount);
			int isPeephole = (fromId >= inputCount + previousOutputCount) && (fromId < inputCount + previousOutputCount + cellsPerBlock);
			int isFromBiasUnit = fromId == (inputCount + previousOutputCount + cellsPerBlock);

			float inputFromWeight = isFromInputUnit * input[isFromInputUnit * fromId]
									+ isFromPreviousOutputUnit * previousOutput[isFromPreviousOutputUnit * (fromId - inputCount)]
									+ isPeephole * cellStates[isPeephole * (toId * cellsPerBlock + (fromId - inputCount - previousOutputCount))]
									+ isFromBiasUnit * 1;
			float outputGateWeightGradient = outputGateDeltas[toId] * inputFromWeight;

			//calculate input and forget gate weight gradients
			float inputGateWeightGradient = 0;
			float forgetGateWeightGradient = 0;
			//loop through cells
			for (int cellId = toId * cellsPerBlock; cellId < (toId + 1) * cellsPerBlock; cellId++)
			{
				inputGateWeightGradient += cellStateErrors[cellId] * inputGateWeightsRTRLPartials[cellId * weightsPerGate + fromId];
				forgetGateWeightGradient += cellStateErrors[cellId] * forgetGateWeightsRTRLPartials[cellId * weightsPerGate + fromId];
			}

			//update gate weights
			if (backPropMethod == RMSProp)
			{
				RMSPropWeightUpdate(trainingRate, momentum, smoothingFactor, outputGateWeights, outputGateWeightDeltas, outputGateWeightMeanSquares, weightId, outputGateWeightGradient);
				RMSPropWeightUpdate(trainingRate, momentum, smoothingFactor, inputGateWeights, inputGateWeightDeltas, inputGateWeightMeanSquares, weightId, inputGateWeightGradient);
				RMSPropWeightUpdate(trainingRate, momentum, smoothingFactor, forgetGateWeights, forgetGateWeightDeltas, forgetGateWeightMeanSquares, weightId, forgetGateWeightGradient);
			}
			else // SGD
			{
				SGDWeightUpdate(trainingRate, momentum, outputGateWeights, outputGateWeightDeltas, weightId, outputGateWeightGradient);
				SGDWeightUpdate(trainingRate, momentum, inputGateWeights, inputGateWeightDeltas, weightId, inputGateWeightGradient);
				SGDWeightUpdate(trainingRate, momentum, forgetGateWeights, forgetGateWeightDeltas, weightId, forgetGateWeightGradient);
			}
		}
	}

	__global__ void LSTMUpdateCellWeightsKernel(
		float *input,
		float *previousOutput,
		float *cellStateErrors,
		float *cellInputWeights,
		float *cellInputWeightDeltas,
		float *cellInputWeightMeanSquares,
		float *cellWeightsRTRLPartials,

		MyBackPropMethod backPropMethod,
		float trainingRate,
		float momentum,
		float smoothingFactor,

		int inputCount,
		int previousOutputCount,
		int cellsPerBlock
		)
	{
		int weightId = blockDim.x * blockIdx.y * gridDim.x	//rows preceeding current row in grid
			+ blockDim.x * blockIdx.x				//blocks preceeding current block
			+ threadIdx.x;

		int weightsPerCell = inputCount + previousOutputCount + 1;
		
		if (weightId < weightsPerCell * previousOutputCount)
		{
			int cellId = weightId / weightsPerCell;
			if (backPropMethod == RMSProp)
			{
				RMSPropWeightUpdate(trainingRate, momentum, smoothingFactor, cellInputWeights, cellInputWeightDeltas, cellInputWeightMeanSquares, weightId, cellStateErrors[cellId] * cellWeightsRTRLPartials[weightId]);
			}
			else
			{
				SGDWeightUpdate(trainingRate, momentum, cellInputWeights, cellInputWeightDeltas, weightId, cellStateErrors[cellId] * cellWeightsRTRLPartials[weightId]);
			}
		}
	}
}
