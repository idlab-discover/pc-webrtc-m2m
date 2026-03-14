package main

import "strconv"

func FillMatrix(tokens []string, offset int) [4][4]float32 {
	m := [4][4]float32{}
	for i := 0; i < 4; i++ {
		for j := 0; j < 4; j++ {
			mm, _ := strconv.ParseFloat(tokens[offset+j+(i*4)], 32)
			m[i][j] = float32(mm)
			//		fmt.Printf("%f\t", float32(mm))
		}
		//	fmt.Printf("\n")
	}
	return m
}

func FillPosition(tokens []string, offset int) [3]float32 {
	p := [3]float32{}
	for i := 0; i < 3; i++ {
		pp, _ := strconv.ParseFloat(tokens[offset+i], 32)
		p[i] = float32(pp)
		//fmt.Printf("%s %f %f %f\n", tokens[offset+i], pp, float32(pp), p[i])
	}
	return p
}

func MultiplyPoint(m [4][4]float32, p [3]float32) [3]float32 {
	x := m[0][0]*p[0] + m[0][1]*p[1] + m[0][2]*p[2] + m[0][3]
	y := m[1][0]*p[0] + m[1][1]*p[1] + m[1][2]*p[2] + m[1][3]
	z := m[2][0]*p[0] + m[2][1]*p[1] + m[2][2]*p[2] + m[2][3]

	return [3]float32{x, y, z}
}

func ConvertToClipspace(m [4][4]float32, p [3]float32) [4]float32 {
	x := m[0][0]*p[0] + m[0][1]*p[1] + m[0][2]*p[2] + m[0][3]
	y := m[1][0]*p[0] + m[1][1]*p[1] + m[1][2]*p[2] + m[1][3]
	z := m[2][0]*p[0] + m[2][1]*p[1] + m[2][2]*p[2] + m[2][3]
	w := m[3][0]*p[0] + m[3][1]*p[1] + m[3][2]*p[2] + m[3][3]
	return [4]float32{x, y, z, w}
}
