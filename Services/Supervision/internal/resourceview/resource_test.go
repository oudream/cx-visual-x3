package resourceview

import (
	"errors"
	"math"
	"testing"
)

func TestCPURejectsUnavailableInvalidAndMulticoreSamples(t *testing.T) {
	for _, tc := range []struct {
		values []float64
		err error
		ok bool
	}{
		{[]float64{23.5}, nil, true},
		{[]float64{0}, nil, true},
		{[]float64{100}, nil, true},
		{nil, nil, false},
		{[]float64{25, 40}, nil, false},
		{[]float64{-1}, nil, false},
		{[]float64{101}, nil, false},
		{[]float64{math.NaN()}, nil, false},
		{[]float64{math.Inf(1)}, nil, false},
		{[]float64{40}, errors.New("unavailable"), false},
	} {
		_, ok := validCPU(tc.values, tc.err)
		if ok != tc.ok { t.Fatalf("values=%v error=%v got=%t want=%t", tc.values, tc.err, ok, tc.ok) }
	}
}

func TestStorageRejectsInconsistentFacts(t *testing.T) {
	for _, tc := range []struct {
		total, used, free uint64
		valid bool
	}{
		{1000, 250, 750, true},
		{1000, 0, 1000, true},
		{0, 0, 0, false},
		{1000, 1001, 0, false},
		{1000, 0, 1001, false},
	} {
		out := storageSample(tc.total,tc.used,tc.free,"2026-10-30T00:00:00Z")
		if out.Available != tc.valid { t.Fatalf("%+v: got %+v",tc,out) }
		if out.SampledAt=="" { t.Fatal("sampling timestamp missing") }
		if tc.total==1000 && tc.used==250 && out.UsedPercent!=25 {
			t.Fatalf("incorrect percentage: %+v",out)
		}
	}
}

func TestHostResourceSampleScopeAndFreshness(t *testing.T) {
	report := Collect(t.TempDir())
	if report.Scope!="host" || report.DiskScope!="work-dir-filesystem" {
		t.Fatalf("resource source and disk scope must be explicit: %+v",report)
	}
	if report.CPU.SampledAt=="" || report.Memory.SampledAt=="" || report.Disk.SampledAt=="" {
		t.Fatal("sampling timestamps required even on error")
	}
	if report.Memory.Available && (report.Memory.TotalBytes==0 || report.Memory.UsedPercent>100) {
		t.Fatalf("invalid available memory: %+v",report.Memory)
	}
	if report.Disk.Available && (report.Disk.TotalBytes==0 || report.Disk.UsedPercent>100) {
		t.Fatalf("invalid available disk usage: %+v",report.Disk)
	}
	if !report.CPU.Available && report.CPU.Error=="" { t.Fatal("missing CPU unavailable reason") }
	if !report.Memory.Available && report.Memory.Error=="" { t.Fatal("missing memory unavailable reason") }
	if !report.Disk.Available && report.Disk.Error=="" { t.Fatal("missing disk unavailable reason") }
}

func TestMissingDiskDirectoryShowsUnknownNotZero(t *testing.T) {
	report := Collect(t.TempDir()+"/never-existed")
	if report.Disk.Available || report.Disk.Error=="" {
		t.Fatalf("nonexistent target filesystem must not be reported as zero usage: %+v",report.Disk)
	}
}
